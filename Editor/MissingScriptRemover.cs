using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kogane.Internal
{
	/// <summary>
	/// シーンやプレハブから Missing Script を削除するエディタ拡張
	/// </summary>
	internal static class MissingScriptRemover
	{
		//================================================================================
		// 定数
		//================================================================================
		private const string ITEM_NAME_ROOT                       = "Edit/UniMissingScriptRemover/";
		private const string ITEM_NAME_REMOVE_FROM_CURRENT_SCENE  = "開いているシーンから Missing Script を削除";
		private const string ITEM_NAME_REMOVE_FROM_ALL_SCENES     = "すべてのシーンから Missing Script を削除";
		private const string ITEM_NAME_REMOVE_FROM_CURRENT_PREFAB = "開いているプレハブから Missing Script を削除";
		private const string ITEM_NAME_REMOVE_FROM_ALL_PREFABS    = "すべてのプレハブから Missing Script を削除";
		private const string PACKAGE_NAME                         = "UniMissingScriptRemover";

		//================================================================================
		// 関数(static)
		//================================================================================
		/// <summary>
		/// 開いているシーンから Missing Script を削除するメニュー
		/// </summary>
		[MenuItem( ITEM_NAME_ROOT + ITEM_NAME_REMOVE_FROM_CURRENT_SCENE )]
		private static void RemoveFromCurrentScene()
		{
			if ( !OpenOkCancelDialog( $"{ITEM_NAME_REMOVE_FROM_CURRENT_SCENE}しますか？" ) ) return;

			var scene = SceneManager.GetActiveScene();

			RemoveFromScene( scene );

			OpenOkDialog( $"{ITEM_NAME_REMOVE_FROM_CURRENT_SCENE}しました" );
		}

		/// <summary>
		/// すべてのシーンから Missing Script を削除するメニュー
		/// </summary>
		[MenuItem( ITEM_NAME_ROOT + ITEM_NAME_REMOVE_FROM_ALL_SCENES )]
		private static void RemoveFromAllScenes()
		{
			if ( !OpenOkCancelDialog( $"{ITEM_NAME_REMOVE_FROM_ALL_SCENES}しますか？" ) ) return;

			var sceneSetups = EditorSceneManager.GetSceneManagerSetup();

			// FindAssets は Packages フォルダも対象になっているので
			// Assets フォルダ以下のシーンのみを抽出
			var scenePaths = AssetDatabase
					.FindAssets( "t:scene" )
					.Select( x => AssetDatabase.GUIDToAssetPath( x ) )
					.Where( x => x.StartsWith( "Assets/" ) )
					.ToArray()
				;

			var count = scenePaths.Length;

			try
			{
				for ( var i = 0; i < count; i++ )
				{
					var num       = i + 1;
					var progress  = ( float ) num / count;
					var scenePath = scenePaths[ i ];
					var scene     = EditorSceneManager.OpenScene( scenePath );

					EditorUtility.DisplayProgressBar
					(
						ITEM_NAME_REMOVE_FROM_ALL_SCENES,
						$"{num}/{count} {scenePath}",
						progress
					);

					RemoveFromScene( scene );
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();

				// Untitled なシーンは復元できず、SceneSetup[] の要素数が 0 になる
				// Untitled なシーンを復元しようとすると下記のエラーが発生するので if で確認
				// ArgumentException: Invalid SceneManagerSetup:
				if ( 0 < sceneSetups.Length )
				{
					EditorSceneManager.RestoreSceneManagerSetup( sceneSetups );
				}
			}

			OpenOkDialog( $"{ITEM_NAME_REMOVE_FROM_ALL_SCENES}しました" );
		}

		/// <summary>
		/// 開いているプレハブから Missing Script を削除するメニュー
		/// </summary>
		[MenuItem( ITEM_NAME_ROOT + ITEM_NAME_REMOVE_FROM_CURRENT_PREFAB )]
		private static void RemoveFromCurrentPrefab()
		{
			if ( !OpenOkCancelDialog( $"{ITEM_NAME_REMOVE_FROM_CURRENT_PREFAB}しますか？" ) ) return;

			var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			RemoveFromPrefab( prefabStage.prefabContentsRoot );

			OpenOkDialog( $"{ITEM_NAME_REMOVE_FROM_CURRENT_PREFAB}しました" );
		}

		/// <summary>
		/// 開いているプレハブから Missing Script を削除するメニューが有効かどうかを返します
		/// </summary>
		[MenuItem( ITEM_NAME_ROOT + ITEM_NAME_REMOVE_FROM_CURRENT_PREFAB, true )]
		private static bool CanRemoveFromCurrentPrefab()
		{
			return PrefabStageUtility.GetCurrentPrefabStage() != null;
		}

		/// <summary>
		/// すべてのプレハブから Missing Script を削除するメニュー
		/// </summary>
		[MenuItem( ITEM_NAME_ROOT + ITEM_NAME_REMOVE_FROM_ALL_PREFABS )]
		private static void RemoveFromAllPrefabs()
		{
			if ( !OpenOkCancelDialog( $"{ITEM_NAME_REMOVE_FROM_ALL_PREFABS}しますか？" ) ) return;

			// FindAssets は Packages フォルダも対象になっているので
			// Assets フォルダ以下のプレハブのみを抽出
			var prefabPaths = AssetDatabase
					.FindAssets( "t:prefab" )
					.Select( x => AssetDatabase.GUIDToAssetPath( x ) )
					.Where( x => x.StartsWith( "Assets/" ) )
					.ToArray()
				;

			var count = prefabPaths.Length;

			try
			{
				for ( var i = 0; i < count; i++ )
				{
					var num        = i + 1;
					var progress   = ( float ) num / count;
					var prefabPath = prefabPaths[ i ];
					var prefab     = PrefabUtility.LoadPrefabContents( prefabPath );

					EditorUtility.DisplayProgressBar
					(
						ITEM_NAME_REMOVE_FROM_ALL_PREFABS,
						$"{num}/{count} {prefabPath}",
						progress
					);

					RemoveFromPrefab( prefab );

					PrefabUtility.SaveAsPrefabAsset( prefab, prefabPath );
					PrefabUtility.UnloadPrefabContents( prefab );
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();

				// この処理を呼び出さないと Project ビューの表示が更新されず、
				// 変更も保存されない
				AssetDatabase.SaveAssets();
			}

			OpenOkDialog( $"{ITEM_NAME_REMOVE_FROM_ALL_PREFABS}しました" );
		}

		/// <summary>
		/// 指定されたシーンから Missing Script を削除するメニュー
		/// </summary>
		private static void RemoveFromScene( Scene scene )
		{
			// シーンに配置しているプレハブのインスタンスから Missing Script を削除しても
			// シーンに保存されないため、プレハブのインスタンスを除外する処理は不要
			var gameObjects = scene
					.GetRootGameObjects()
					.SelectMany( x => x.GetComponentsInChildren<Transform>( true ) )
					.Select( x => x.gameObject )
					.Where( x => 0 < GameObjectUtility.GetMonoBehavioursWithMissingScriptCount( x ) )
					.ToArray()
				;

			if ( gameObjects.Length <= 0 ) return;

			foreach ( var gameObject in gameObjects )
			{
				GameObjectUtility.RemoveMonoBehavioursWithMissingScript( gameObject );
			}

			EditorSceneManager.MarkSceneDirty( scene );
			EditorSceneManager.SaveScene( scene );
		}

		/// <summary>
		/// 指定されたプレハブから Missing Script を削除するメニュー
		/// </summary>
		private static void RemoveFromPrefab( GameObject prefab )
		{
			var gameObjects = prefab
					.GetComponentsInChildren<Transform>( true )
					.Select( x => x.gameObject )
					.Where( x => 0 < GameObjectUtility.GetMonoBehavioursWithMissingScriptCount( x ) )
					.ToArray()
				;

			if ( gameObjects.Length <= 0 ) return;

			foreach ( var gameObject in gameObjects )
			{
				GameObjectUtility.RemoveMonoBehavioursWithMissingScript( gameObject );
			}
		}

		/// <summary>
		/// OK キャンセルダイアログを開く
		/// </summary>
		private static bool OpenOkCancelDialog( string message )
		{
			return EditorUtility.DisplayDialog
			(
				title: PACKAGE_NAME,
				message: message,
				ok: "OK",
				cancel: "キャンセル"
			);
		}

		/// <summary>
		/// OK ダイアログを開く
		/// </summary>
		private static void OpenOkDialog( string message )
		{
			EditorUtility.DisplayDialog
			(
				title: PACKAGE_NAME,
				message: message,
				ok: "OK"
			);
		}
	}
}