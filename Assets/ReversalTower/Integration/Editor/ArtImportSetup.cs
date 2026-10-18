using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ReversalTower.Integration.EditorTools
{
    /// <summary>
    /// 받아온 아트 에셋을 Unity가 쓸 수 있는 형태로 한 번에 정리한다.
    ///
    ///   역전의 탑 ▸ 아트 ▸ ★ 스프라이트 슬라이스 · 애니메이션 만들기
    ///
    /// 하는 일
    ///   1. 텍스처 임포트 설정 (픽셀아트용: Point 필터, 압축 없음, Multiple 모드, PPU)
    ///   2. 격자 슬라이스 — 기사 66×72 / 공격 120×90 / 이펙트 64×64
    ///      기사는 피벗을 '아래 가운데'로 둔다. 프레임 크기가 달라도 발 위치가 안 흔들린다.
    ///   3. 애니메이션 클립 생성 (Assets/Data/Boss/Arakus/Anim)
    ///   4. 애니메이터 컨트롤러 생성 (Arakus.controller)
    ///   5. 이펙트 프리팹 생성 (Assets/Data/Effects) — 빈 프레임은 자동으로 잘라낸다
    ///
    /// 다시 눌러도 안전하다. 이미 있는 것은 덮어쓴다.
    /// </summary>
    public static class ArtImportSetup
    {
        const string KnightFolder = "Assets/MyAssets/RoyalKnight_x3";
        const string EffectFolder = "Assets/MyAssets/Pixel Holy Spell Effect 32x32 Pack 3";
        const string AnimFolder = "Assets/Data/Boss/Arakus/Anim";
        const string EffectPrefabFolder = "Assets/Data/Effects";
        public const string ControllerPath = "Assets/Data/Boss/Arakus/Arakus.controller";

        // 기사: 원본 22×24 / 40×30 을 Scale3x 한 것
        const int KnightW = 66, KnightH = 72;
        const int AttackW = 120, AttackH = 90;
        const int KnightPPU = 28;      // 캐릭터 63px → 약 2.25m
        const int EffectCell = 64;
        const int EffectPPU = 32;      // 64px → 2m

        static readonly string[] AttackFiles = { "Attack_1", "Attack_2", "Attack_2_Recover", "Attack_3" };

        /// <summary>클립 이름 → (파일, 초당 프레임, 반복)</summary>
        static readonly (string clip, string file, float fps, bool loop)[] Clips =
        {
            ("Idle",        "Combat_Ready_Idle",  8f, true),
            ("Walk",        "Walk",              10f, true),
            ("Run",         "Run",               12f, true),
            ("Attack1",     "Attack_1",          14f, false),
            ("Attack2",     "Attack_2",          14f, false),
            ("Attack3",     "Attack_3",          14f, false),
            ("Hurt",        "Hit_Front",         14f, false),
            ("Roll",        "Roll",              16f, false),
            ("ShieldRaise", "Shield_Raise",      10f, false),
            ("ShieldWalk",  "Shield_Raise_Walk", 10f, true),
            ("SwordRaise",  "Sword_Raise",       12f, false),
            ("Channel",     "Sword_Channel",      8f, true),
        };

        /// <summary>한 번만 재생하고 Idle로 돌아오는 상태 → 그걸 부르는 트리거 이름</summary>
        static readonly (string state, string trigger)[] OneShots =
        {
            ("Attack1",     "Attack"),
            ("Attack2",     "Attack2"),
            ("Attack3",     "Attack3"),
            ("SwordRaise",  "BeamAttack"),
            ("Roll",        "BackStep"),
            ("ShieldWalk",  "ShieldCharge"),
            ("Channel",     "Channel"),
            ("Hurt",        "Hurt"),
        };

        [MenuItem("역전의 탑/아트/★ 스프라이트 슬라이스 · 애니메이션 만들기", false, 10)]
        public static void RunAll()
        {
            if (!Directory.Exists(KnightFolder))
            {
                EditorUtility.DisplayDialog("역전의 탑",
                    $"기사 스프라이트 폴더가 없습니다:\n{KnightFolder}\n\n" +
                    "깃에서 최신 main을 받아 주세요.", "확인");
                return;
            }

            EnsureFolder(AnimFolder);
            EnsureFolder(EffectPrefabFolder);

            try
            {
                AssetDatabase.StartAssetEditing();
                SliceKnight();
                SliceEffects();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            int clips = BuildClips();
            var controller = BuildController();
            int fx = BuildEffectPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg =
                "아트 세팅 완료\n\n" +
                $"기사 스프라이트   {Clips.Length}개 동작 슬라이스\n" +
                $"애니메이션 클립   {clips}개  ({AnimFolder})\n" +
                $"애니메이터        {(controller != null ? Path.GetFileName(ControllerPath) : "실패")}\n" +
                $"이펙트 프리팹     {fx}개  ({EffectPrefabFolder})\n\n" +
                "다음: 역전의 탑 ▸ ★ 2. 게임 흐름에 아라쿠스 넣기 를 다시 눌러 주세요.";
            Debug.Log("[아트 세팅] " + msg.Replace("\n\n", "\n"));
            EditorUtility.DisplayDialog("역전의 탑", msg, "좋아요");
        }

        // ─────────────────────── 슬라이스 ───────────────────────

        static void SliceKnight()
        {
            foreach (string path in Directory.GetFiles(KnightFolder, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                bool isAttack = AttackFiles.Contains(name);
                int cw = isAttack ? AttackW : KnightW;
                int ch = isAttack ? AttackH : KnightH;
                SliceGrid(path.Replace('\\', '/'), cw, ch, KnightPPU, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f));
            }
        }

        static void SliceEffects()
        {
            if (!Directory.Exists(EffectFolder)) return;
            foreach (string path in Directory.GetFiles(EffectFolder, "*.png"))
                SliceGrid(path.Replace('\\', '/'), EffectCell, EffectCell, EffectPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f), readable: true);
        }

        /// <summary>격자로 자르고 픽셀아트용 임포트 설정을 넣는다.</summary>
        static void SliceGrid(string path, int cellW, int cellH, int ppu, SpriteAlignment align, Vector2 pivot, bool readable = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;        // 픽셀아트 — 흐려지지 않게
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = ppu;
            importer.isReadable = readable;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            // 텍스처 크기를 알아야 격자를 계산할 수 있다
            importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
            int cols = texW / cellW, rows = texH / cellH;
            if (cols <= 0 || rows <= 0) return;

            string baseName = Path.GetFileNameWithoutExtension(path);

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            var pairs = new List<SpriteNameFileIdPair>();
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Unity 텍스처 좌표는 왼쪽 아래가 원점 — 위에서부터 세려고 뒤집는다
                    var rect = new Rect(c * cellW, texH - (r + 1) * cellH, cellW, cellH);
                    string spriteName = rows > 1 ? $"{baseName}_{r}_{c}" : $"{baseName}_{index}";
                    var sr = new SpriteRect
                    {
                        name = spriteName,
                        spriteID = GUID.Generate(),
                        rect = rect,
                        alignment = align,
                        pivot = pivot,
                        border = Vector4.zero,
                    };
                    rects.Add(sr);
                    pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
                    index++;
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null) nameIds.SetNameFileIdPairs(pairs);
            provider.Apply();

            importer.SaveAndReimport();
        }

        // ─────────────────────── 애니메이션 클립 ───────────────────────

        static int BuildClips()
        {
            int made = 0;
            foreach (var (clipName, file, fps, loop) in Clips)
            {
                var sprites = LoadSprites($"{KnightFolder}/{file}.png");
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[아트 세팅] 스프라이트를 찾지 못했습니다: {file}.png");
                    continue;
                }

                var clip = new AnimationClip { frameRate = fps };
                var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                var keys = new ObjectReferenceKeyframe[sprites.Count];
                for (int i = 0; i < sprites.Count; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

                var s = AnimationUtility.GetAnimationClipSettings(clip);
                s.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, s);

                string path = $"{AnimFolder}/{clipName}.anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(clip, path);
                made++;
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        static List<Sprite> LoadSprites(string texturePath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(texturePath)
                .OfType<Sprite>()
                .OrderBy(s => SuffixNumber(s.name))
                .ToList();
        }

        static int SuffixNumber(string name)
        {
            int i = name.LastIndexOf('_');
            return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
        }

        // ─────────────────────── 애니메이터 ───────────────────────

        static AnimatorController BuildController()
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var (_, trigger) in OneShots)
                ac.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();

            foreach (var (clipName, _, _, _) in Clips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{clipName}.anim");
                if (clip == null) continue;
                var st = sm.AddState(clipName);
                st.motion = clip;
                states[clipName] = st;
            }

            if (!states.TryGetValue("Idle", out var idle)) return ac;
            sm.defaultState = idle;

            // 걷기 전환
            if (states.TryGetValue("Walk", out var walk))
            {
                var toWalk = idle.AddTransition(walk);
                toWalk.hasExitTime = false; toWalk.duration = 0.05f;
                toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

                var toIdle = walk.AddTransition(idle);
                toIdle.hasExitTime = false; toIdle.duration = 0.05f;
                toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            // 한 번짜리 동작: 어디서든 트리거로 들어가고, 끝나면 Idle로
            foreach (var (stateName, trigger) in OneShots)
            {
                if (!states.TryGetValue(stateName, out var st)) continue;

                var enter = sm.AddAnyStateTransition(st);
                enter.hasExitTime = false;
                enter.duration = 0f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

                var exit = st.AddTransition(idle);
                exit.hasExitTime = true;
                exit.exitTime = 0.92f;
                exit.duration = 0.06f;
            }

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ─────────────────────── 이펙트 프리팹 ───────────────────────

        /// <summary>시트의 각 행을 이펙트 하나로 보고 프리팹을 만든다. 빈 프레임은 잘라낸다.</summary>
        static int BuildEffectPrefabs()
        {
            if (!Directory.Exists(EffectFolder)) return 0;
            int made = 0;

            foreach (string path in Directory.GetFiles(EffectFolder, "*.png").Select(p => p.Replace('\\', '/')))
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
                if (sprites.Count == 0) continue;

                string baseName = Path.GetFileNameWithoutExtension(path);
                var rows = sprites
                    .Select(s => new { s, rc = ParseRowCol(s.name) })
                    .Where(x => x.rc.row >= 0)
                    .GroupBy(x => x.rc.row)
                    .OrderBy(g => g.Key);

                foreach (var row in rows)
                {
                    var frames = row.OrderBy(x => x.rc.col)
                                    .Select(x => x.s)
                                    .Where(HasPixels)
                                    .ToArray();
                    if (frames.Length < 2) continue;

                    string prefabPath = $"{EffectPrefabFolder}/FX_{baseName}_{row.Key}.prefab";
                    var go = new GameObject($"FX_{baseName}_{row.Key}");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = frames[0];
                    sr.sortingOrder = 30;
                    var fx = go.AddComponent<SpriteSheetEffect>();
                    fx.frames = frames;
                    fx.fps = 18f;
                    fx.loop = false;
                    fx.destroyOnFinish = true;

                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Object.DestroyImmediate(go);
                    made++;
                }
            }
            return made;
        }

        static (int row, int col) ParseRowCol(string name)
        {
            var parts = name.Split('_');
            if (parts.Length >= 3
                && int.TryParse(parts[parts.Length - 2], out int r)
                && int.TryParse(parts[parts.Length - 1], out int c))
                return (r, c);
            return (-1, -1);
        }

        /// <summary>완전히 투명한 프레임인지 검사 (시트 끝의 빈 칸 제거용)</summary>
        static bool HasPixels(Sprite s)
        {
            if (s == null || s.texture == null) return false;
            try
            {
                var r = s.textureRect;
                var pixels = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                foreach (var p in pixels) if (p.a > 0.01f) return true;
                return false;
            }
            catch
            {
                return true; // 읽을 수 없으면 일단 포함
            }
        }

        // ─────────────────────── 도우미 ───────────────────────

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
