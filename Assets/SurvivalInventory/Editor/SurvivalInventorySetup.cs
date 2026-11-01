using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurvivalInventory.EditorTools
{
    public static class SurvivalInventorySetup
    {
        const string MenuRoot = "Tools/Survival Inventory/";

        struct Def
        {
            public string id, name, abbr, desc, useLabel;
            public ItemCategory category;
            public ItemRarity rarity;
            public int maxStack, startCount;
            public float weight;
            public bool usable, consume;
        }

        static readonly Def[] Defs =
        {
            new Def { id = "cloth", name = "천", abbr = "천", category = ItemCategory.Material,
                maxStack = 20, weight = 0.1f, startCount = 6,
                desc = "낡은 천 조각. 붕대를 만들거나 무언가를 감쌀 때 쓴다." },

            new Def { id = "bandage", name = "붕대", abbr = "붕대", category = ItemCategory.Medical,
                maxStack = 10, weight = 0.05f, startCount = 3,
                usable = true, consume = true, useLabel = "치료",
                desc = "상처를 감아 피를 멈춘다. 사용하면 1개 줄어든다." },

            new Def { id = "fuel", name = "연료통", abbr = "연료통", category = ItemCategory.Resource, rarity = ItemRarity.Rare,
                maxStack = 5, weight = 1.0f, startCount = 2,
                desc = "발전기나 난로에 넣는 연료 한 통. 무겁다." },

            new Def { id = "battery", name = "배터리", abbr = "배터리", category = ItemCategory.Resource,
                maxStack = 10, weight = 0.1f, startCount = 4,
                desc = "손전등 같은 전자 기기에 넣는다." },

            new Def { id = "flashlight", name = "손전등", abbr = "손전등", category = ItemCategory.Tool,
                maxStack = 1, weight = 0.4f, startCount = 1,
                usable = true, consume = false, useLabel = "켜기",
                desc = "어둠 속을 비춘다. 배터리가 있어야 켜진다." },

            new Def { id = "bag", name = "가방", abbr = "가방", category = ItemCategory.Equipment, rarity = ItemRarity.Rare,
                maxStack = 1, weight = 1.2f, startCount = 1,
                usable = true, consume = false, useLabel = "메기",
                desc = "등에 메는 천 가방. 메면 더 많은 짐을 들 수 있다." },

            new Def { id = "metal", name = "고철", abbr = "고철", category = ItemCategory.Material,
                maxStack = 20, weight = 0.5f, startCount = 5,
                desc = "녹슨 금속 조각. 수리나 제작 재료로 쓴다." },
        };

        [MenuItem(MenuRoot + "1. 기본 아이템 7종 만들기 · 갱신")]
        public static void CreateItemsMenu()
        {
            if (BlockedInPlayMode()) return;
            var items = CreateItems();
            Selection.activeObject = items[0];
            Debug.Log($"[Survival Inventory] 아이템 에셋 {items.Length}개 준비 완료: {GetBaseFolder()}/Items");

            // 씬의 시작 아이템 목록은 건드리지 않고, 예전 버전 목록이면 알려만 준다 (일부러 뺀 아이템을 되살리지 않도록)
            foreach (var inventory in Object.FindObjectsByType<Inventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                PlanSync(inventory, items, out int missing, out var absent);
                if (missing > 0 || absent.Count > 0)
                    Debug.LogWarning($"[Survival Inventory] '{inventory.name}'의 시작 아이템 목록이 예전 버전입니다" +
                                     $" (빈 칸 {missing}개, 빠진 기본 아이템: {(absent.Count > 0 ? string.Join(", ", absent) : "없음")})." +
                                     " 맞추려면 메뉴 3을 누르세요.");
            }
        }

        // 예전 버전으로 배치한 'Inventory UI'의 시작 아이템 목록은 씬에 저장되어 있어서, 아이템 에셋만 갱신하면
        // 새로 생긴 기본 아이템(예: 고철)이 빠진 채로 남는다. 직접 누를 때만 열린 씬의 목록을 맞춘다 (Ctrl+Z로 되돌릴 수 있음).
        [MenuItem(MenuRoot + "3. 열린 씬의 시작 아이템을 기본 7종에 맞추기")]
        public static void SyncStartingItemsMenu()
        {
            if (BlockedInPlayMode()) return;
            var items = CreateItems();
            int changedCount = 0;
            foreach (var inventory in Object.FindObjectsByType<Inventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                PlanSync(inventory, items, out int missing, out var absent);
                if (missing == 0 && absent.Count == 0) continue;

                Undo.RecordObject(inventory, "시작 아이템 맞추기");
                var so = new SerializedObject(inventory);
                var list = so.FindProperty("startingItems");
                for (int i = list.arraySize - 1; i >= 0; i--)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == null)
                        list.DeleteArrayElementAtIndex(i);
                for (int i = 0; i < items.Length; i++)
                {
                    if (!absent.Contains(items[i].displayName)) continue;
                    list.arraySize++;
                    var el = list.GetArrayElementAtIndex(list.arraySize - 1);
                    el.FindPropertyRelative("item").objectReferenceValue = items[i];
                    el.FindPropertyRelative("count").intValue = Defs[i].startCount;
                }
                so.ApplyModifiedProperties();
                if (PrefabUtility.IsPartOfPrefabInstance(inventory))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
                EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);
                changedCount++;
                Debug.Log($"[Survival Inventory] '{inventory.name}' 시작 아이템 정리: 빈 칸 {missing}개 제거" +
                          (absent.Count > 0 ? $", 추가 {string.Join(", ", absent)}" : "") + " — 씬을 저장하세요. (Ctrl+Z로 되돌리기)");
            }
            if (changedCount == 0) Debug.Log("[Survival Inventory] 열린 씬의 시작 아이템 목록이 이미 최신입니다.");
        }

        // 빈 칸(Missing) 수와, 목록에 없는 기본 아이템 이름을 센다
        static void PlanSync(Inventory inventory, ItemData[] items, out int missing, out System.Collections.Generic.List<string> absent)
        {
            var so = new SerializedObject(inventory);
            var list = so.FindProperty("startingItems");
            var present = new System.Collections.Generic.HashSet<string>();
            missing = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue as ItemData;
                if (item == null) missing++;
                else present.Add(item.id);
            }
            absent = new System.Collections.Generic.List<string>();
            foreach (var item in items)
                if (!present.Contains(item.id)) absent.Add(item.displayName);
        }

        static bool BlockedInPlayMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EditorUtility.DisplayDialog("Survival Inventory", "Play 모드에서는 바꿔도 저장되지 않습니다. Play를 멈춘 뒤 다시 눌러 주세요.", "확인");
            return true;
        }

        [MenuItem(MenuRoot + "2. 현재 씬에 인벤토리 UI 배치")]
        public static void SetupSceneMenu()
        {
            if (BlockedInPlayMode()) return;

            // 게임 씬(Build Settings에 등록된 씬)에는 게임 UI와 Tab·Esc 키가 겹치므로 먼저 확인한다
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path != activeScene.path) continue;
                if (!EditorUtility.DisplayDialog("Survival Inventory",
                        $"'{activeScene.name}'은(는) 게임 씬입니다.\n게임 UI와 Tab·Esc 키가 겹쳐 조작이 꼬일 수 있습니다.\n그래도 배치할까요? (테스트는 Demo/InventoryDemo 씬을 권장)",
                        "배치", "취소"))
                    return;
                break;
            }

            string baseFolder = GetBaseFolder();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{baseFolder}/UI/Inventory.uxml");
            if (uxml == null)
            {
                EditorUtility.DisplayDialog("Survival Inventory",
                    "UI/Inventory.uxml 을 찾지 못했습니다.\nSurvivalInventory 폴더 구조를 그대로 Assets 안에 두었는지 확인하세요.", "확인");
                return;
            }

            var items = CreateItems();
            var panelSettings = GetOrCreatePanelSettings(baseFolder);

            var go = new GameObject("Inventory UI");
            Undo.RegisterCreatedObjectUndo(go, "Create Inventory UI");

            var inventory = go.AddComponent<Inventory>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;
            var ui = go.AddComponent<InventoryUI>();

            var invSo = new SerializedObject(inventory);
            var list = invSo.FindProperty("startingItems");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("item").objectReferenceValue = items[i];
                el.FindPropertyRelative("count").intValue = Defs[i].startCount;
            }
            invSo.ApplyModifiedPropertiesWithoutUndo();

            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("inventory").objectReferenceValue = inventory;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Survival Inventory] 'Inventory UI' 오브젝트를 배치했습니다. Play를 눌러 확인하세요.");
        }

        // ───────────────────────────────

        static ItemData[] CreateItems()
        {
            string baseFolder = GetBaseFolder();
            string folder = baseFolder + "/Items";
            EnsureFolder(folder);

            var result = new ItemData[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                var d = Defs[i];
                string path = $"{folder}/{d.id}.asset";

                // 이미 있으면 사용자가 수정한 값을 지키기 위해 덮어쓰지 않는다
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemData>();
                    item.id = d.id;
                    item.displayName = d.name;
                    item.shortLabel = d.abbr;
                    item.description = d.desc;
                    item.category = d.category;
                    item.rarity = d.rarity;
                    item.maxStack = d.maxStack;
                    item.weight = d.weight;
                    item.usable = d.usable;
                    item.consumeOnUse = d.consume;
                    item.useLabel = string.IsNullOrEmpty(d.useLabel) ? "사용" : d.useLabel;
                    item.icon = LoadIcon(baseFolder, d.id);
                    AssetDatabase.CreateAsset(item, path);
                }
                else
                {
                    // 이미 있는 기본 아이템은 이름·등급·아이콘만 최신으로 맞춘다 (설명, 수치는 그대로)
                    item.displayName = d.name;
                    item.shortLabel = d.abbr;
                    item.rarity = d.rarity;
                    var icon = LoadIcon(baseFolder, d.id);
                    if (icon != null) item.icon = icon;
                    EditorUtility.SetDirty(item);
                }
                result[i] = item;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        static Sprite LoadIcon(string baseFolder, string id)
        {
            string path = $"{baseFolder}/Icons/{id}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;

            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static PanelSettings GetOrCreatePanelSettings(string baseFolder)
        {
            string path = $"{baseFolder}/UI/InventoryPanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps != null)
            {
                // 테마 파일(Assets/UI Toolkit)이 빠진 채 공유되면 글자·버튼이 그려지지 않으므로 다시 연결한다
                if (ps.themeStyleSheet == null)
                {
                    ps.themeStyleSheet = FindOrCreateTheme(baseFolder);
                    EditorUtility.SetDirty(ps);
                    AssetDatabase.SaveAssets();
                }
                return ps;
            }

            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.themeStyleSheet = FindOrCreateTheme(baseFolder);
            AssetDatabase.CreateAsset(ps, path);
            AssetDatabase.SaveAssets();
            return ps;
        }

        static ThemeStyleSheet FindOrCreateTheme(string baseFolder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ThemeStyleSheet"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/"))
                    return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(p);
            }

            // 프로젝트에 런타임 테마가 없으면 Unity 기본 테마를 불러오는 tss를 만든다
            string path = $"{baseFolder}/UI/InventoryRuntimeTheme.tss";
            File.WriteAllText(path, "@import url(\"unity-theme://default\");\nVisualElement {}\n");
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
        }

        static string GetBaseFolder()
        {
            foreach (var guid in AssetDatabase.FindAssets("Inventory t:VisualTreeAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (p.EndsWith("/UI/Inventory.uxml"))
                    return Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');
            }
            return "Assets/SurvivalInventory";
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
