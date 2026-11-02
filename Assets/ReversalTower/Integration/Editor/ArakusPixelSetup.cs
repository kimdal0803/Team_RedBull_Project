using System.IO;
using System.Linq;
using ReversalTower.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReversalTower.Integration.EditorTools
{
    /// <summary>
    /// 아라쿠스 도트 스프라이트를 씬의 보스에 입힌다.
    ///
    ///   1. 스프라이트 세트 만들기   — Assets/MyAssets/Arakus/Sprites의 시트로 Arakus_SpriteSet 에셋을 만든다
    ///   2. 씬의 아라쿠스에 입히기  — 원래 그림(Dark Knight 등)은 끄기만 하고, 도트 그림 + BossSpriteAnimator를 붙인다
    ///   되돌리기                    — 도트 그림을 지우고 원래 그림을 다시 켠다
    ///
    /// 패턴별 '언제 어느 프레임'은 아래 Defaults 표에 있다. 세트 에셋에서 직접 고쳐도 되고,
    /// 다시 '1'을 눌러도 고친 타이밍은 지워지지 않는다 (그림만 갱신). 처음 값으로 돌리려면 '타이밍 초기화'.
    /// </summary>
    public static class ArakusPixelSetup
    {
        const string SpriteDir = "Assets/MyAssets/Arakus/Sprites";
        const string SetPath = "Assets/Data/Boss/Arakus/Arakus_SpriteSet.asset";
        const string PixelRootName = "VisualPixel";
        const string BodyName = "Body";
        const string MenuRoot = "역전의 탑/아라쿠스 도트/";

        // ─────────────── 패턴별 기본 타이밍 ───────────────

        struct Def
        {
            public string patternId, sheet;
            public SpriteKey[] keys;
            public bool arc;
            public SpriteKeyEvent arcFrom, arcTo;
            public float arcHeight;
        }

        static SpriteKey K(SpriteKeyEvent at, float offset, int frame, bool punch = false, int shake = 0, int hit = 0)
        {
            return new SpriteKey { at = at, offset = offset, frame = frame, punch = punch, shake = shake, hitIndex = hit };
        }

        /// <summary>판정마다: 판정 0.05초 전 타격 프레임 → afterOffset 뒤 다음 프레임 (마지막 타 뒤엔 복귀 프레임)</summary>
        static SpriteKey Each(float strikeOffset, int strikeFrame, float afterOffset, int afterFrame, int lastAfterFrame, int shake)
        {
            return new SpriteKey
            {
                at = SpriteKeyEvent.EachHit, offset = strikeOffset, frame = strikeFrame, punch = true, shake = shake,
                afterOffset = afterOffset, afterFrame = afterFrame, lastAfterFrame = lastAfterFrame,
            };
        }

        static Def D(string id, string sheet, params SpriteKey[] keys) => new Def { patternId = id, sheet = sheet, keys = keys };

        static Def Arc(Def d, SpriteKeyEvent from, SpriteKeyEvent to, float height)
        {
            d.arc = true;
            d.arcFrom = from;
            d.arcTo = to;
            d.arcHeight = height;
            return d;
        }

        const SpriteKeyEvent Start = SpriteKeyEvent.Start, Attack = SpriteKeyEvent.Attack, Recovery = SpriteKeyEvent.Recovery,
                             DashStart = SpriteKeyEvent.DashStart;

        // 시트의 프레임 순서는 README_ARAKUS_SPRITES.md 표 참고
        static Def[] Defaults() => new[]
        {
            // 일반공격 1: 칼을 뒤로 당기며 낮춤 → 횡베기 → 복귀
            D("Arakus_NormalAttack_R", "Arakus_NormalAttack_R_PT", K(Start, 0f, 0), Each(-0.05f, 1, 0.22f, 2, 2, 1)),
            // 일반공격 2: 방패를 머리 위로 → 내려찍기(세게 흔들림) → 복귀
            D("Arakus_NormalAttack_L", "Arakus_NormalAttack_L_PT", K(Start, 0f, 0), Each(-0.05f, 1, 0.25f, 2, 2, 2)),
            // 일반공격 3: 다리를 들고 뒤로 젖힘 → 걷어참 → 복귀
            D("Arakus_NormalAttack_F", "Arakus_NormalAttack_F_PT", K(Start, 0f, 0), Each(-0.05f, 1, 0.20f, 2, 2, 1)),
            // 거검 휘두르기: 어깨에 검을 멤 → 내려벰 → 복귀
            D("Arakus_SwordSwinging", "Arakus_Sword_Swinging_PT", K(Start, 0f, 0), Each(-0.05f, 1, 0.30f, 2, 2, 2)),
            // 퀵스텝: 순간 낮춤 → 뒤로 뜀(작은 점프) → 착지
            Arc(D("Arakus_QuickStep", "Arakus_QuickStep_PT",
                  K(Start, 0f, 0), K(Attack, 0f, 1, true), K(Recovery, 0f, 2, true)), Attack, Recovery, 0.35f),
            // 방패 투척 후 대쉬: 던질 준비 → 던지고 달려듦 → (대쉬 끝) 베기 → 복귀
            D("Arakus_ShieldThrowDash", "Arakus_ShieldThrowing_DashAttack_PT",
              K(Start, 0f, 0), K(Attack, 0f, 1, true), K(Recovery, -0.05f, 1, true, 1), K(Recovery, 0.15f, 2)),
            // 방패돌진: 낮춰 준비 → 방패를 앞세워 돌진 → 제동
            D("Arakus_ShieldDash", "Arakus_ShieldDash_PT", K(Start, 0f, 0), K(Attack, 0f, 1, true), K(Recovery, 0f, 2, true)),
            // 방패 던지고 내려찍기: 던질 준비 → 놓음 → 도약(큰 점프) → 방패 받고 낙하 → 내려찍기(세게 흔들림)
            Arc(D("Arakus_ShieldThrowSlam", "Arakus_Shield_Throwing_Fly_Attack_PT",
                  K(Start, 0f, 0), K(Attack, 0f, 1, true), K(DashStart, 0f, 2), K(Recovery, -0.12f, 3), K(Recovery, 0f, 4, true, 2)),
                DashStart, Recovery, 1.8f),
            // 일대 휩쓸기: 검에 기를 모음 → (휩쓸기 → 다시 듦) × 타수 → 복귀
            D("Arakus_SweepingStrike", "Arakus_Aggressive_Attack_PT", K(Start, 0f, 0), Each(-0.05f, 1, 0.25f, 2, 3, 1)),
            // 아라쿠스의 분노: 무릎 꿇고 기를 모음 → 버프 순간 부풀림(세게 흔들림) → 복귀
            D("Arakus_Rage", "Arakus_Of_Wrath_PT", K(Start, 0f, 0), K(Attack, 0f, 1, true, 2), K(Attack, 0.45f, 2)),
            // 돌진: 낮춰 준비 → 돌진 → 복귀
            D("Arakus_Charge", "Arakus_Charge_PT", K(Start, 0f, 0), K(Attack, 0f, 1, true, 1), K(Recovery, 0f, 2)),
        };

        // ─────────────── 메뉴 ───────────────

        [MenuItem(MenuRoot + "1. 스프라이트 세트 만들기 (갱신)", false, 30)]
        static void MenuBuild()
        {
            var set = BuildOrUpdateSet(false);
            if (set == null) return;
            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
        }

        [MenuItem(MenuRoot + "2. 씬의 아라쿠스에 입히기", false, 31)]
        static void MenuAttach()
        {
            var boss = FindArakus();
            if (boss == null)
            {
                EditorUtility.DisplayDialog("아라쿠스 도트",
                    "씬에서 아라쿠스(BossController, bossId = Arakus)를 찾지 못했습니다.\n" +
                    "먼저 '역전의 탑 ▸ ★ 1. 아라쿠스 테스트 씬 만들기'로 씬을 열거나, 보스 오브젝트를 선택하고 다시 눌러 주세요.", "확인");
                return;
            }
            var set = BuildOrUpdateSet(false);
            if (set == null) return;
            Attach(boss, set);
        }

        [MenuItem(MenuRoot + "타이밍 초기화 (세트의 키를 처음 값으로)", false, 45)]
        static void MenuResetTimings()
        {
            if (!EditorUtility.DisplayDialog("아라쿠스 도트", "세트 에셋의 패턴별 타이밍(키, 점프 곡선)을 처음 값으로 되돌립니다.", "되돌리기", "취소"))
                return;
            BuildOrUpdateSet(true);
        }

        [MenuItem(MenuRoot + "되돌리기 (원래 그림으로)", false, 50)]
        static void MenuRevert()
        {
            var boss = FindArakus();
            if (boss == null) return;
            Revert(boss);
        }

        // ─────────────── 세트 만들기 ───────────────

        public static BossSpriteSet BuildOrUpdateSet(bool resetTimings)
        {
            var idle = LoadFrames("Arakus_Idle");
            if (idle.Length == 0)
            {
                EditorUtility.DisplayDialog("아라쿠스 도트", $"{SpriteDir}/Arakus_Idle.png 를 찾지 못했습니다.\n이 브랜치의 스프라이트가 받아졌는지 확인해 주세요.", "확인");
                return null;
            }

            var set = AssetDatabase.LoadAssetAtPath<BossSpriteSet>(SetPath);
            if (set == null)
            {
                EnsureFolder(Path.GetDirectoryName(SetPath).Replace('\\', '/'));
                set = ScriptableObject.CreateInstance<BossSpriteSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            Undo.RecordObject(set, "아라쿠스 도트 세트 갱신");

            set.idle = idle;
            set.idleOrder = new[] { 0, 1, 2, 1 };
            set.groggy = LoadFrames("Arakus_Groggy");
            set.projectileSprite = AssetDatabase.LoadAllAssetsAtPath($"{SpriteDir}/Arakus_Shield_Projectile.png").OfType<Sprite>().FirstOrDefault();

            var missing = new System.Collections.Generic.List<string>();
            foreach (var d in Defaults())
            {
                var entry = set.Find(d.patternId);
                bool fresh = entry == null;
                if (fresh)
                {
                    entry = new BossSpriteAnim { patternId = d.patternId };
                    set.patterns.Add(entry);
                }
                if (fresh || resetTimings)
                {
                    entry.keys = d.keys.ToList();
                    entry.arc = d.arc;
                    entry.arcFrom = d.arcFrom;
                    entry.arcTo = d.arcTo;
                    entry.arcHeight = d.arcHeight;
                    entry.tremble = true;
                }
                entry.sheetName = d.sheet;
                entry.frames = LoadFrames(d.sheet);
                if (entry.frames.Length == 0) missing.Add(d.sheet);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            if (missing.Count > 0)
                Debug.LogWarning("[아라쿠스 도트] 시트를 찾지 못함: " + string.Join(", ", missing), set);
            else
                Debug.Log($"[아라쿠스 도트] 세트 갱신 완료 — 패턴 {set.patterns.Count}개, 대기 {set.idle.Length}프레임 ({SetPath})", set);
            return set;
        }

        static Sprite[] LoadFrames(string sheet)
        {
            var path = $"{SpriteDir}/{sheet}.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            sprites.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
            return sprites.ToArray();
        }

        static int FrameIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int i) ? i : 0;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ─────────────── 씬에 입히기 / 되돌리기 ───────────────

        static BossController FindArakus()
        {
            var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<BossController>() : null;
            if (sel != null) return sel;
            // 씬에 있는 것만 (프리팹 에셋 제외). 꺼진 오브젝트도 찾는다
            var all = Resources.FindObjectsOfTypeAll<BossController>()
                .Where(b => b != null && b.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(b))
                .ToArray();
            return all.FirstOrDefault(b => b.bossId == "Arakus") ?? (all.Length == 1 ? all[0] : null);
        }

        static void Attach(BossController boss, BossSpriteSet set)
        {
            Undo.SetCurrentGroupName("아라쿠스 도트 입히기");
            int group = Undo.GetCurrentGroup();

            var animator = boss.GetComponent<BossSpriteAnimator>();
            if (animator == null) animator = Undo.AddComponent<BossSpriteAnimator>(boss.gameObject);
            Undo.RecordObject(animator, "아라쿠스 도트 입히기");
            Undo.RecordObject(boss, "아라쿠스 도트 입히기");

            // 1) 도트 그림 오브젝트: 보스 ▸ VisualPixel ▸ Body
            var root = boss.transform.Find(PixelRootName);
            if (root == null)
            {
                var go = new GameObject(PixelRootName);
                Undo.RegisterCreatedObjectUndo(go, "아라쿠스 도트 입히기");
                go.layer = boss.gameObject.layer;
                root = go.transform;
                root.SetParent(boss.transform, false);
            }
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var bodyT = root.Find(BodyName);
            if (bodyT == null)
            {
                var go = new GameObject(BodyName);
                Undo.RegisterCreatedObjectUndo(go, "아라쿠스 도트 입히기");
                go.layer = boss.gameObject.layer;
                bodyT = go.transform;
                bodyT.SetParent(root, false);
            }
            var sr = bodyT.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(bodyT.gameObject);
            Undo.RecordObject(sr, "아라쿠스 도트 입히기");
            sr.sprite = set.idle.Length > 0 ? set.idle[0] : null;

            // 2) 원래 그림은 지우지 않고 끄기만 (그리기 순서는 물려받음)
            var old = boss.visualRoot != null && boss.visualRoot != root ? boss.visualRoot.gameObject : null;
            if (old == null)
            {
                var named = boss.transform.Find("Visual");
                if (named != null) old = named.gameObject;
            }
            if (old != null)
            {
                var oldSr = old.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
                if (oldSr != null)
                {
                    sr.sortingLayerID = oldSr.sortingLayerID;
                    sr.sortingOrder = oldSr.sortingOrder;
                }
                animator.previousVisual = old;
                if (old.activeSelf)
                {
                    Undo.RecordObject(old, "아라쿠스 도트 입히기");
                    old.SetActive(false);
                }
            }
            else if (sr.sortingOrder == 0)
            {
                sr.sortingOrder = 5;
            }

            // 3) 보스가 돌아설 때 뒤집히는 대상 = VisualPixel. 애니메이터 트리거는 더 이상 쓰지 않는다
            boss.visualRoot = root;
            boss.animator = null;
            var binder = boss.GetComponent<BossVisualBinder>();
            if (binder != null)
            {
                Undo.RecordObject(binder, "아라쿠스 도트 입히기");
                binder.animator = null;
            }

            animator.spriteSet = set;
            animator.body = sr;

            // 4) 던지는 방패 그림
            int shields = 0;
            if (set.projectileSprite != null && boss.patterns != null)
            {
                foreach (var p in boss.patterns)
                {
                    if (p == null || p.kind != PatternKind.Projectile || p.projectile == null) continue;
                    Undo.RecordObject(p, "아라쿠스 도트 입히기");
                    p.projectile.sprite = set.projectileSprite;
                    EditorUtility.SetDirty(p);
                    shields++;
                }
            }

            EditorUtility.SetDirty(boss);
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = bodyT.gameObject;

            // 세트에 없는 패턴 알려 주기
            var noAnim = boss.patterns == null ? new string[0] :
                boss.patterns.Where(p => p != null && set.Find(p.patternId) == null).Select(p => p.patternId).ToArray();

            EditorUtility.DisplayDialog("아라쿠스 도트",
                $"'{boss.name}'에 도트 그림을 입혔습니다.\n\n" +
                $"원래 그림        {(old != null ? $"'{old.name}' 끔 (지우지 않음)" : "없음")}\n" +
                $"던지는 방패      패턴 {shields}개에 연결\n" +
                $"그림이 없는 패턴  {(noAnim.Length == 0 ? "없음" : string.Join(", ", noAnim) + " → 대기 그림으로 보임")}\n\n" +
                "씬을 저장하고 Play 해 보세요. 되돌리려면 '아라쿠스 도트 ▸ 되돌리기'.", "확인");
        }

        static void Revert(BossController boss)
        {
            Undo.SetCurrentGroupName("아라쿠스 도트 되돌리기");
            int group = Undo.GetCurrentGroup();

            var animator = boss.GetComponent<BossSpriteAnimator>();
            var root = boss.transform.Find(PixelRootName);
            var prev = animator != null ? animator.previousVisual : null;
            if (prev == null)
            {
                var named = boss.transform.Find("Visual");
                if (named != null) prev = named.gameObject;
            }
            var shield = animator != null && animator.spriteSet != null ? animator.spriteSet.projectileSprite : null;

            Undo.RecordObject(boss, "아라쿠스 도트 되돌리기");
            if (prev != null)
            {
                Undo.RecordObject(prev, "아라쿠스 도트 되돌리기");
                prev.SetActive(true);
                boss.visualRoot = prev.transform;
                boss.animator = prev.GetComponentInChildren<Animator>(true);
                var binder = boss.GetComponent<BossVisualBinder>();
                if (binder != null)
                {
                    Undo.RecordObject(binder, "아라쿠스 도트 되돌리기");
                    binder.animator = boss.animator;
                }
            }
            else
            {
                boss.visualRoot = null;
            }

            if (shield != null && boss.patterns != null)
            {
                foreach (var p in boss.patterns)
                {
                    if (p == null || p.projectile == null || p.projectile.sprite != shield) continue;
                    Undo.RecordObject(p, "아라쿠스 도트 되돌리기");
                    p.projectile.sprite = null;
                    EditorUtility.SetDirty(p);
                }
            }

            if (root != null) Undo.DestroyObjectImmediate(root.gameObject);
            if (animator != null) Undo.DestroyObjectImmediate(animator);

            EditorUtility.SetDirty(boss);
            EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log($"[아라쿠스 도트] '{boss.name}'를 원래 그림으로 되돌렸습니다.", boss);
        }
    }
}
