﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEditor;
using System.IO;
using AnimationImporter.Boomlagoon.JSON;
using UnityEditor.Animations;
using System.Linq;

namespace AnimationImporter
{
	public class AnimationImporterWindow : EditorWindow
	{
		// ================================================================================
		//  private
		// --------------------------------------------------------------------------------

		private AnimationImporter importer
		{
			get
			{
				return AnimationImporter.Instance;
			}
		}

		private GUIStyle _dropBoxStyle;
		private GUIStyle _infoTextStyle;

		private string _nonLoopingAnimationEnterValue = "";

		private Vector2 _scrollPos = Vector2.zero;

		// ================================================================================
		//  menu entry
		// --------------------------------------------------------------------------------

		[MenuItem("Window/Animation Importer")]
		public static void ImportAnimationsMenu()
		{
			GetWindow(typeof(AnimationImporterWindow), false, "Anim Importer");
		}

		// ================================================================================
		//  unity methods
		// --------------------------------------------------------------------------------

		public void OnEnable()
		{
			importer.LoadOrCreateUserConfig();
		}

		public void OnGUI()
		{
			CheckGUIStyles();

			if (importer.canImportAnimations)
			{
				_scrollPos = GUILayout.BeginScrollView(_scrollPos);

				EditorGUILayout.Space();

				ShowAnimationsGUI();

				GUILayout.Space(25f);

				ShowAnimatorControllerGUI();

				GUILayout.Space(25f);

				ShowAnimatorOverrideControllerGUI();

				GUILayout.Space(25f);

				ShowUserConfig();

				GUILayout.EndScrollView();
			}
			else
			{
				EditorGUILayout.Space();

				ShowHeadline("Select Aseprite Application");

				EditorGUILayout.Space();

				ShowAsepriteApplicationSelection();

				EditorGUILayout.Space();

				GUILayout.Label("Aseprite has to be installed on this machine because the Importer calls Aseprite through the command line for creating images and getting animation data.", _infoTextStyle);
			}
		}

		// ================================================================================
		//  GUI methods
		// --------------------------------------------------------------------------------

		private void CheckGUIStyles()
		{
			if (_dropBoxStyle == null)
			{
				GetBoxStyle();
			}
			if (_infoTextStyle == null)
			{
				GetTextInfoStyle();
			}
		}

		private void GetBoxStyle()
		{
			_dropBoxStyle = new GUIStyle(EditorStyles.helpBox);
			_dropBoxStyle.alignment = TextAnchor.MiddleCenter;
		}

		private void GetTextInfoStyle()
		{
			_infoTextStyle = new GUIStyle(EditorStyles.label);
			_infoTextStyle.wordWrap = true;
		}

		private void ShowUserConfig()
		{
			if (importer == null || importer.sharedData == null)
			{
				return;
			}

			ShowHeadline("Config");

			/*
				Aseprite Application
			*/

			ShowAsepriteApplicationSelection();

			GUILayout.Space(5f);

			/*
				sprite values
			*/

			importer.sharedData.targetObjectType = (AnimationTargetObjectType)EditorGUILayout.EnumPopup("Target Object", importer.sharedData.targetObjectType);

			importer.sharedData.spriteAlignment = (SpriteAlignment)EditorGUILayout.EnumPopup("Sprite Alignment", importer.sharedData.spriteAlignment);

			if (importer.sharedData.spriteAlignment == SpriteAlignment.Custom)
			{
				importer.sharedData.spriteAlignmentCustomX = EditorGUILayout.Slider("x", importer.sharedData.spriteAlignmentCustomX, 0, 1f);
				importer.sharedData.spriteAlignmentCustomY = EditorGUILayout.Slider("y", importer.sharedData.spriteAlignmentCustomY, 0, 1f);
			}

			importer.sharedData.spritePixelsPerUnit = EditorGUILayout.FloatField("Sprite Pixels per Unit", importer.sharedData.spritePixelsPerUnit);

			EditorGUILayout.BeginHorizontal();
			importer.sharedData.saveSpritesToSubfolder = EditorGUILayout.Toggle("Sprites to Subfolder", importer.sharedData.saveSpritesToSubfolder);

			importer.sharedData.saveAnimationsToSubfolder = EditorGUILayout.Toggle("Animations to Subfolder", importer.sharedData.saveAnimationsToSubfolder);
			EditorGUILayout.EndHorizontal();

			GUILayout.Space(25f);

			ShowHeadline("Automatic Import");
			EditorGUILayout.BeginHorizontal();
			importer.sharedData.automaticImporting = EditorGUILayout.Toggle("Automatic Import", importer.sharedData.automaticImporting);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField("Looks for existing Animation Controller with same name.");

			/*
				animations that do not loop
			*/

			GUILayout.Space(25f);
			ShowHeadline("Non-looping Animations");

			for (int i = 0; i < importer.sharedData.animationNamesThatDoNotLoop.Count; i++)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(importer.sharedData.animationNamesThatDoNotLoop[i]);
				bool doDelete = GUILayout.Button("Delete");
				GUILayout.EndHorizontal();
				if (doDelete)
				{
					importer.sharedData.RemoveAnimationThatDoesNotLoop(i);
					break;
				}
			}

			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Add ");
			_nonLoopingAnimationEnterValue = EditorGUILayout.TextField(_nonLoopingAnimationEnterValue);
			if (GUILayout.Button("Enter"))
			{
				if (importer.sharedData.AddAnimationThatDoesNotLoop(_nonLoopingAnimationEnterValue))
				{
					_nonLoopingAnimationEnterValue = "";
				}
			}
			GUILayout.EndHorizontal();

			EditorGUILayout.LabelField("Enter Part of the Animation Name or a Regex Expression.");

			if (GUI.changed)
			{
				EditorUtility.SetDirty(importer.sharedData);
			}
		}

		private void ShowAsepriteApplicationSelection()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Aseprite Application Path");

			string newPath = importer.asepritePath;

			if (GUILayout.Button("Select"))
			{
				var path = EditorUtility.OpenFilePanel(
					"Select Aseprite Application",
					"",
					"exe");
				if (!string.IsNullOrEmpty(path))
				{
					newPath = path;

					if (Application.platform == RuntimePlatform.OSXEditor)
					{
						newPath += "/Contents/MacOS/aseprite";
					}
				}
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			importer.asepritePath = GUILayout.TextField(newPath, GUILayout.MaxWidth(300f));

			GUILayout.EndHorizontal();
		}

		private void ShowAnimationsGUI()
		{
			ShowHeadline("Animations");

			DefaultAsset[] droppedAssets = ShowDropButton<DefaultAsset>(importer.canImportAnimations, AnimationImporter.IsValidAsset);
			if (droppedAssets != null)
			{
				try
				{
					for (int i = 0; i < droppedAssets.Length; i++)
					{
						EditorUtility.DisplayProgressBar("Import Animations", "Importing...", (float)i / droppedAssets.Length);
						importer.CreateAnimationsForAssetFile(droppedAssets[i]);
					}
					AssetDatabase.Refresh();
				}
				catch (Exception error)
				{
					Debug.LogWarning(error.ToString());
					throw;
				}

				EditorUtility.ClearProgressBar();
			}
		}

		private void ShowAnimatorControllerGUI()
		{
			ShowHeadline("Animator Controller + Animations");

			DefaultAsset[] droppedAssets = ShowDropButton<DefaultAsset>(importer.canImportAnimations, AnimationImporter.IsValidAsset);
			if (droppedAssets != null)
			{
				try
				{
					for (int i = 0; i < droppedAssets.Length; i++)
					{
						EditorUtility.DisplayProgressBar("Import Animator Controller", "Importing...", (float)i / droppedAssets.Length);

						var animationInfo = importer.CreateAnimationsForAssetFile(droppedAssets[i]);

						if (animationInfo != null && animationInfo.hasAnimations)
						{
							importer.CreateAnimatorController(animationInfo);
						}
					}

					AssetDatabase.Refresh();
				}
				catch (Exception error)
				{
					Debug.LogWarning(error.ToString());
					throw;
				}

				EditorUtility.ClearProgressBar();
			}
		}

		private void ShowAnimatorOverrideControllerGUI()
		{
			ShowHeadline("Animator Override Controller + Animations");

			importer.baseController = EditorGUILayout.ObjectField("Based on Controller:", importer.baseController, typeof(RuntimeAnimatorController), false) as RuntimeAnimatorController;

			DefaultAsset[] droppedAssets = ShowDropButton<DefaultAsset>(importer.canImportAnimationsForOverrideController, AnimationImporter.IsValidAsset);
			if (droppedAssets != null)
			{
				try
				{
					for (int i = 0; i < droppedAssets.Length; i++)
					{
						EditorUtility.DisplayProgressBar("Import Animator Override Controller", "Importing...", (float)i / droppedAssets.Length);

						var animationInfo = importer.CreateAnimationsForAssetFile(droppedAssets[i]);

						if (animationInfo != null && animationInfo.hasAnimations)
						{
							importer.CreateAnimatorOverrideController(animationInfo);
						}
					}

					AssetDatabase.Refresh();
				}
				catch (Exception error)
				{
					Debug.LogWarning(error.ToString());
					throw;
				}

				EditorUtility.ClearProgressBar();
			}
		}

		private void ShowHeadline(string headline)
		{
			EditorGUILayout.LabelField(headline, EditorStyles.boldLabel, GUILayout.Height(20f));
		}

		// ================================================================================
		//  OnGUI helper
		// --------------------------------------------------------------------------------

		public delegate bool IsValidAssetDelegate(string path);

		private T[] ShowDropButton<T>(bool isEnabled, IsValidAssetDelegate IsValidAsset) where T : UnityEngine.Object
		{
			T[] returnValue = null;

			Rect drop_area = GUILayoutUtility.GetRect(0.0f, 80.0f, GUILayout.ExpandWidth(true));

			GUI.enabled = isEnabled;
			GUI.Box(drop_area, "Drop Animation file here", _dropBoxStyle);
			GUI.enabled = true;

			if (!isEnabled)
				return null;

			Event evt = Event.current;
			switch (evt.type)
			{
				case EventType.DragUpdated:
				case EventType.DragPerform:

					if (!drop_area.Contains(evt.mousePosition)
						|| !DraggedObjectsContainValidObject<T>(IsValidAsset))
						return null;

					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						DragAndDrop.AcceptDrag();

						List<T> validObjects = new List<T>();

						foreach (UnityEngine.Object dragged_object in DragAndDrop.objectReferences)
						{
							var assetPath = AssetDatabase.GetAssetPath(dragged_object);

							if (dragged_object is T && IsValidAsset(assetPath))
							{
								validObjects.Add(dragged_object as T);
							}
						}

						returnValue = validObjects.ToArray();
					}

					evt.Use();

					break;
			}

			return returnValue;
		}

		private bool DraggedObjectsContainValidObject<T>(IsValidAssetDelegate IsValidAsset) where T : UnityEngine.Object
		{
			foreach (UnityEngine.Object dragged_object in DragAndDrop.objectReferences)
			{
				var assetPath = AssetDatabase.GetAssetPath(dragged_object);

				if (dragged_object is T && IsValidAsset(assetPath))
				{
					return true;
				}
			}

			return false;
		}
	}
}
