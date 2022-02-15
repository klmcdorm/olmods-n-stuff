using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(Overload.MenuManager), "BriefingUpdate")]
    public class MenuManager_BriefingUpdate_CustomCutscene
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            int state = 0;

            foreach (var i in code)
            {
                switch (state)
                {
                    // Find Resources.Load preceded by "Cutscenes/" + var1 (level filename)
                    case 0:
                        if (i.Is(OpCodes.Ldstr, "Cutscenes/"))
                        {
                            state = 1;
                        }
                        yield return i;
                        break;
                    // Replace Resources.Load with custom function
                    case 1:
                        if (i.Calls(AccessTools.Method(typeof(Resources), "Load", new Type[] { typeof(string) })))
                        {
                            yield return new CodeInstruction(OpCodes.Call,
                                AccessTools.Method(typeof(MenuManager_BriefingUpdate_CustomCutscene),
                                "LoadCutscene", new Type[] { typeof(string) }));
                            state = 2;
                        }
                        else
                        {
                            yield return i;
                        }
                        break;
                    default:
                        yield return i;
                        break;
                }
            }
        }

        public static Transform SearchForChildByName(Transform t, string name)
        {
            if (t == null) { return null; }

            var count = t.childCount;
            for (int i = 0; i < count; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name)
                {
                    return c;
                }

                if (c.childCount > 0)
                {
                    var cc = SearchForChildByName(c, name);
                    if (cc != null) { return cc; }
                }
            }
            return null;
        }

        public static GameObject LoadCutscene(string name)
        {
            // TODO allow custom Outro? (by default it will only try to load cutscene_black)
            var result = Resources.Load(name);
            if (result != null) { return (GameObject)result; }

            AssetBundle bundle = null;
            IUserFileSystem fs = null;
            try
            {
                var shortName = (name.StartsWith("Cutscenes/") ? name.Substring(10) : name);

                if (Overload.GameplayManager.Level.ZipPath != null)
                {
                    Debug.Log("Trying to load cutscene from custom mission zip " + Overload.GameplayManager.Level.Mission.ZipPath);
                    fs = new ZipUserFileSystem(Overload.GameplayManager.Level.Mission.ZipPath, "");
                }
                else
                {
                    Debug.Log("Trying to load cutscene from custom mission folder " + Overload.GameplayManager.Level.Mission.FolderPath);
                    fs = new RawUserFileSystem(Overload.GameplayManager.Level.Mission.FolderPath);
                }

                // TODO variable assets path and filename (but where would it be configured?)
                using (var stream = fs.OpenFileStreamToMemory(System.IO.Path.Combine(System.IO.Path.Combine("cutscenes", OsFolderName()), "assets")))
                {
                    bundle = AssetBundle.LoadFromStream(stream);

                    var prefab = bundle.LoadAsset<GameObject>(shortName);
                    if (prefab == null) { return null; }

                    if (prefab.GetComponent<CutsceneController>() == null)
                    {
                        //UnityEngine.Debug.Log("Add CutsceneController");
                        var cam = prefab.GetComponentInChildren<Camera>();

                        var ctrl = prefab.AddComponent<CutsceneController>();
                        ctrl.m_flyby_sound = CutsceneController.FlybySound.NONE;
                        ctrl.m_black_anim = 0.0f;
                        ctrl.m_white_anim = 0.0f;
                        ctrl.m_main_camera = new Camera[0]; // not really needed, controller will search for cameras

                        if (cam != null)
                        {
                            var uiQuadByName = SearchForChildByName(prefab.transform, "ui_quad")?.gameObject;
                            if (uiQuadByName == null)
                            {
                                Debug.Log("Adding default UI quad");
                                uiQuadByName = AddUiQuad(cam.gameObject);
                            }

                            var renderer = uiQuadByName.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                // This is a render texture which displays cutscene text.
                                // TODO figure out proper path instead of searching
                                var mats = Resources.FindObjectsOfTypeAll<Material>();
                                var mat = mats.FirstOrDefault(m => m.name == "ui_material1");
                                //var cut2 = UnityEngine.Resources.Load<UnityEngine.GameObject>("Cutscenes/cutscene_black");
                                //var renderer2 = cut2.GetComponent<CutsceneController>().m_ui_quad[0].GetComponent<UnityEngine.MeshRenderer>();
                                //var mat = renderer2.material;
                                //UnityEngine.Debug.Log("material " + mat.name);
                                renderer.material = mat;
                            }
                            ctrl.m_ui_quad = new GameObject[] { uiQuadByName };
                        }
                    }
                    return prefab;
                }
            }
            catch (Exception e)
            {
                Debug.Log("LoadCustomCutscene ERROR " + e.Message);
                return null;
            }
            finally
            {
                fs?.Dispose();
                bundle?.Unload(false);
            }
        }

        public static string OsFolderName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "osx";
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return "linux";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return "windows";
                default:
                    // TODO what else does OLmod support? what kind of asset bundles would they need?
                    throw new Exception();
            }
        }

        public static GameObject AddUiQuad(GameObject camera)
        {
            var scale = new GameObject();
            scale.transform.SetParent(camera.transform, false);
            scale.transform.localScale = new Vector3(0.3f, 0.3f, 0.4f);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(quad.GetComponent<Rigidbody>()); // probably not needed but Debug log suggests it's there
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "ui_quad";
            quad.layer = 29;
            quad.transform.SetParent(scale.transform, false);
            quad.transform.Translate(0.8f * Vector3.forward);
            quad.transform.localScale = 3 * Vector3.one;

            return quad;
        }
    }
}
