using System;
using SEPluginManager;
using HarmonyLib;
using VRageMath;
using VRage.Plugins;
using Sandbox.Game.Gui;
using Sandbox.Game.Entities;
using VRage.Input;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox;
using Sandbox.Game.Entities.Cube;
using VRage.Utils;
using System.Reflection;
using VRage.Game;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces;
using System.Text;
using VRage.Sync;
using Sandbox.Game.Screens.Terminal.Controls;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using System.Linq;
using SpaceEngineers.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using VRage.Audio;
using VRage.Game.Entity;

namespace Xo
{
    enum MODES
    {
        BLOCK = 0,
        BLOCK_COLOR = 1,
        BLOCK_COLOR_PROPERTIES = 2
    }
    public class BlockPicker : SEPMPlugin, IPlugin, IDisposable
    {
        public bool initialized = false;
        public static Logger log;
        private Quaternion quat = new Quaternion();
        private FieldInfo f_m_gizmo;
        private FieldInfo f_m_currentGrid;
        private MODES mode = MODES.BLOCK_COLOR;
        private MyTerminalBlock tmpBlock;
        private string[] modeStrings = new string[3]
        {
            "Block only",
            "Block & Color",
            "Block, Color and Properties"
        };
        List<ITerminalProperty> properties = new List<ITerminalProperty>();
        List<ITerminalProperty> newProperties = new List<ITerminalProperty>();
        public void Main(Harmony harmony, Logger _log)
        {
            log = _log;
            log.Log("Init");
            f_m_gizmo = AccessTools.Field(typeof(MyCubeBuilder), "m_gizmo");
            f_m_currentGrid = AccessTools.Field(typeof(MyCubeBuilder), "m_currentGrid");
            //harmony.Patch(AccessTools.Method(typeof(MySoundBlock), "UpdateSoundEmitters"), new HarmonyMethod(typeof(Patch_FixAudio), "Prefix"));
            //harmony.Patch(AccessTools.Method(typeof(MyEntity3DSoundEmitter), "StopSound"), new HarmonyMethod(typeof(Patch_StopSound), "Prefix"));
        }

        public void Init(object gameObject)
        {
            initialized = true;
            MyCharacter.POINT_LIGHT_RANGE = 0.1f;
            MyCharacter.POINT_LIGHT_INTENSITY = 0.1f;
            MyCharacter.POINT_GLOSS_FACTOR = 0.1f;
            MyCharacter.POINT_DIFFUSE_FACTOR = 0.5f;

            MyCharacter.REFLECTOR_FALLOFF = 0.5f;
            MyCharacter.REFLECTOR_INTENSITY = 12f;
            MyCharacter.LIGHT_PARAMETERS_CHANGED = true;
        }
        public void Update()
        {
            if (!initialized)
            {
                return;
            }
            if (MyCubeBuilder.Static != null && MyInput.Static.IsNewGameControlPressed(MyControlsSpace.BUILD_SCREEN) && MyInput.Static.IsAnyCtrlKeyPressed())
            {
                if (MyGuiScreenGamePlay.DisableInput || !MyCubeBuilder.Static.IsBuildToolActive())
                {
                    return;
                }
                bool flag = MySession.Static.ControlledEntity is MyCockpit && !MyBlockBuilderBase.SpectatorIsBuilding;
                if (flag && MySession.Static.ControlledEntity is MyCockpit && (MySession.Static.ControlledEntity as MyCockpit).BuildingMode)
                {
                    flag = false;
                }
                if (MySandboxGame.IsPaused || flag)
                {
                    return;
                }
                if (MyInput.Static.IsAnyShiftKeyPressed()) {
                    mode++;
                    if (mode > (MySession.Static.IsServer?MODES.BLOCK_COLOR_PROPERTIES:MODES.BLOCK_COLOR))
                    {
                        mode = MODES.BLOCK;
                    }
                    MyHud.Chat.ShowMessage("[BlockPicker]", "Block picker mode set to: " + modeStrings[(int)mode]);
                    return;
                }
                var m_gizmo = (MyCubeBuilderGizmo)f_m_gizmo.GetValue(MyCubeBuilder.Static);
                var currentGrid = (MyCubeGrid)f_m_currentGrid.GetValue(MyCubeBuilder.Static);
                if (m_gizmo == null || currentGrid == null)
                {
                    return;
                }
                foreach (MyCubeBuilderGizmo.MyGizmoSpaceProperties myGizmoSpaceProperties in m_gizmo.Spaces)
                {
                    if (myGizmoSpaceProperties.m_removePos == null)
                    {
                        continue;
                    }
                    var cubeBlock = currentGrid.GetCubeBlock(myGizmoSpaceProperties.m_removePos);
                    if (cubeBlock != null && MySession.Static.LocalHumanPlayer != null)
                    {
                        if (cubeBlock.FatBlock != null && cubeBlock.FatBlock is MyTerminalBlock && mode == MODES.BLOCK_COLOR_PROPERTIES)
                        {
                            var tb = cubeBlock.FatBlock as MyTerminalBlock;
                            tmpBlock = tb;
                            currentGrid.OnFatBlockAdded -= OnFatBlockAdded;
                            currentGrid.OnFatBlockAdded += OnFatBlockAdded;
                            MyCubeBuilder.Static.OnDeactivated -= OnCloseGridBuilder;
                            MyCubeBuilder.Static.OnDeactivated += OnCloseGridBuilder;
                            MyCubeBuilder.Static.OnBlockVariantChanged -= OnCloseGridBuilder;
                            MyCubeBuilder.Static.OnBlockVariantChanged += OnCloseGridBuilder;
                        }
                        MyCubeBuilder.Static.CubeBuilderState.UpdateCubeBlockDefinition(cubeBlock.BlockDefinition.Id, m_gizmo.SpaceDefault.m_localMatrixAdd);

                        cubeBlock.Orientation.GetQuaternion(out quat);
                        m_gizmo.SpaceDefault.m_localMatrixAdd = Matrix.CreateFromQuaternion(quat);
                        if (mode >= MODES.BLOCK_COLOR)
                        {
                            MySession.Static.LocalHumanPlayer.ChangeOrSwitchToColor(cubeBlock.ColorMaskHSV);
                            MyStringHash skinSubtypeId = cubeBlock.SkinSubtypeId;
                            MySession.Static.LocalHumanPlayer.BuildArmorSkin = ((skinSubtypeId != MyStringHash.NullOrEmpty) ? skinSubtypeId.ToString() : string.Empty);
                        }
                        return;

                    }
                }
            }
        }
        public void OnCloseGridBuilder()
        {
            tmpBlock = null;
        }
        public void OnFatBlockAdded(MyCubeBlock newBlock)
        {
            if (tmpBlock == null)
            {
                return;
            } else if (mode != MODES.BLOCK_COLOR_PROPERTIES || !MySession.Static.IsServer)
            {
                tmpBlock = null;
                return;
            }
            log.Log("tmp" + tmpBlock.GetType() + " " + tmpBlock.GetBaseEntity());
            tmpBlock.GetProperties(properties);
            if (newBlock is MyTerminalBlock && properties.Count > 0)
            {
                if (newBlock.GetType() != tmpBlock.GetType())
                {
                    return;
                }
                var newTerminalBlock = newBlock as MyTerminalBlock;
                newProperties.Clear();
                newTerminalBlock.GetProperties(newProperties);

                if (newProperties == null)
                {
                    return;
                }

                var results = new List<ITerminalProperty>();
                MyTerminalControlFactory.GetValueControls(newBlock.GetType(), results);
                foreach (var result in results)
                {
                    log.Log("value id " + result.Id + " " + result.GetType() + " " + result.ToString());
                }
                foreach (var property in newProperties)
                {
                    var p = tmpBlock.GetProperty(property.Id);
                    var np = newTerminalBlock.GetProperty(property.Id);
                    if (p != null)
                    {
                        log.Log(property.Id + " / " + property.TypeName + " " + property.ToString());

                        switch (p.TypeName)
                        {
                            case "Single":
                                np.AsFloat().SetValue(newTerminalBlock, p.AsFloat().GetValue(tmpBlock));

                                //newTerminalBlock.SetValue(p.Id, tmpBlock.GetValue<float>(p.Id));
                                //((IMyTerminalValueControl<float>)p).Setter(newTerminalBlock, tmpBlock.GetValue<float>(p.Id));
                                //((IMyTerminalValueControl<float>)p).Setter.Invoke(newTerminalBlock, tmpBlock.GetValue<float>(p.Id));

                                //((MyTerminalControlSlider<MyLightingBlock>)p).SetValue((MyLightingBlock)newTerminalBlock, tmpBlock.GetValue<float>(p.Id));
                                break;
                            case "Boolean":
                                np.AsBool().SetValue(newTerminalBlock, p.AsBool().GetValue(tmpBlock));
                                //newTerminalBlock.SetValue(p.Id, tmpBlock.GetValue<bool>(p.Id));
                                //((IMyTerminalValueControl<bool>)p).Setter(newTerminalBlock, tmpBlock.GetValue<bool>(p.Id));
                                break;
                            case "Color":
                                np.AsColor().SetValue(newTerminalBlock, p.AsColor().GetValue(tmpBlock));
                                //newTerminalBlock.SetValue(p.Id, tmpBlock.GetValue<Color>(p.Id));
                                //((IMyTerminalValueControl<Color>)p).Setter(newTerminalBlock, tmpBlock.GetValue<Color>(p.Id));
                                //((IMyTerminalValueControl<Color>)p).Setter.Invoke(newTerminalBlock, tmpBlock.GetValue<Color>(p.Id));

                                break;
                            case "StringBuilder":
                                np.As<StringBuilder>().SetValue(newTerminalBlock, p.As<StringBuilder>().GetValue(tmpBlock));
                                //newTerminalBlock.SetValue(p.Id, tmpBlock.GetValue<StringBuilder>(p.Id));
                                //((IMyTerminalValueControl<StringBuilder>)p).Setter(newTerminalBlock, tmpBlock.GetValue<StringBuilder>(p.Id));
                                break;
                            case "Int64":
                                //newTerminalBlock.SetValue(p.Id, tmpBlock.GetValue<Int64>(p.Id));
                                //((IMyTerminalValueControl<Int64>)p).Setter(newTerminalBlock, tmpBlock.GetValue<Int64>(p.Id));
                                np.As<Int64>().SetValue(newTerminalBlock, p.As<Int64>().GetValue(tmpBlock));
                                break;
                        }

                    }
                }
                properties.Clear();
                newProperties.Clear();
                newTerminalBlock.RaisePropertiesChanged();
            }
        }
        public void Dispose()
        {
        }
    }

    /*[HarmonyPatch(typeof(MySoundBlock), "UpdateSoundEmitters")]
    public static class Patch_FixAudio
    {
        public static bool Prefix(MySoundBlock __instance)
        {
            //BlockPicker.log.Log("fix audio");
            bool isPlaying = (bool)typeof(MySoundBlock).GetField("m_isPlaying", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
            if (MyAudio.Static.CanPlay && !Sandbox.Engine.Platform.Game.IsDedicated && isPlaying && !__instance.m_soundEmitter.IsPlaying && __instance.m_soundEmitter.LastSoundData != null && __instance.m_soundEmitter.LastSoundData.Category == MyStringId.GetOrCompute("Music"))
            {
                __instance.RequestPlaySound();
            }
            if (__instance.m_soundEmitter != null)
            {
                __instance.m_soundEmitter.Update();
            }
            return false;
        }
    }

    public static class Patch_StopSound
    {
        public static void Prefix(MyEntity3DSoundEmitter __instance)
        {
            BlockPicker.log.Log("stop sound");
        }
    }*/
}

