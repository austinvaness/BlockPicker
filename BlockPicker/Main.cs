using System;
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
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces;
using System.Text;
using Sandbox.Game.Entities.Character;
using HarmonyLib;
using Sandbox.ModAPI;

namespace Xo
{
    public class BlockPicker : IPlugin, IDisposable
    {
        public bool initialized = false;
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

        public void Init(object gameObject)
        {
            f_m_gizmo = AccessTools.Field(typeof(MyCubeBuilder), "m_gizmo");
            f_m_currentGrid = AccessTools.Field(typeof(MyCubeBuilder), "m_currentGrid");

            initialized = true;
        }

        public void Update()
        {
            if (initialized && MyCubeBuilder.Static != null && MyInput.Static.IsNewGameControlPressed(MyControlsSpace.BUILD_SCREEN) && MyInput.Static.IsAnyCtrlKeyPressed())
            {
                if (MyGuiScreenGamePlay.DisableInput || !MyCubeBuilder.Static.IsBuildToolActive())
                    return;

                bool flag = MySession.Static.ControlledEntity is MyCockpit && !MyBlockBuilderBase.SpectatorIsBuilding;
                if (flag && MySession.Static.ControlledEntity is MyCockpit && (MySession.Static.ControlledEntity as MyCockpit).BuildingMode)
                    flag = false;

                if (MySandboxGame.IsPaused || flag)
                    return;

                if (MyInput.Static.IsAnyShiftKeyPressed())
                {
                    mode++;
                    if (mode > (MySession.Static.IsServer ? MODES.BLOCK_COLOR_PROPERTIES : MODES.BLOCK_COLOR))
                    {
                        mode = MODES.BLOCK;
                    }
                    MyAPIGateway.Utilities.ShowMessage("[BlockPicker]", "Block picker mode set to: " + modeStrings[(int)mode]);
                    return;
                }

                var m_gizmo = (MyCubeBuilderGizmo)f_m_gizmo.GetValue(MyCubeBuilder.Static);
                var currentGrid = (MyCubeGrid)f_m_currentGrid.GetValue(MyCubeBuilder.Static);
                if (m_gizmo == null || currentGrid == null)
                    return;

                foreach (MyCubeBuilderGizmo.MyGizmoSpaceProperties myGizmoSpaceProperties in m_gizmo.Spaces)
                {
                    if (myGizmoSpaceProperties.m_removePos == null)
                        continue;

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

                        cubeBlock.Orientation.GetQuaternion(out Quaternion quat);
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
                return;

            if (mode != MODES.BLOCK_COLOR_PROPERTIES || !MySession.Static.IsServer)
            {
                tmpBlock = null;
                return;
            }

            tmpBlock.GetProperties(properties);
            if (newBlock is MyTerminalBlock && properties.Count > 0)
            {
                if (newBlock.GetType() != tmpBlock.GetType())
                    return;

                MyTerminalBlock newTerminalBlock = newBlock as MyTerminalBlock;
                newProperties.Clear();
                newTerminalBlock.GetProperties(newProperties);

                if (newProperties == null)
                    return;

                List<ITerminalProperty> results = new List<ITerminalProperty>();
                MyTerminalControlFactory.GetValueControls(newBlock.GetType(), results);
                foreach (var property in newProperties)
                {
                    var p = tmpBlock.GetProperty(property.Id);
                    var np = newTerminalBlock.GetProperty(property.Id);
                    if (p != null)
                    {


                        switch (p.TypeName)
                        {
                            case "Single":
                                np.AsFloat().SetValue(newTerminalBlock, p.AsFloat().GetValue(tmpBlock));
                                break;
                            case "Boolean":
                                np.AsBool().SetValue(newTerminalBlock, p.AsBool().GetValue(tmpBlock));
                                break;
                            case "Color":
                                np.AsColor().SetValue(newTerminalBlock, p.AsColor().GetValue(tmpBlock));
                                break;
                            case "StringBuilder":
                                np.As<StringBuilder>().SetValue(newTerminalBlock, p.As<StringBuilder>().GetValue(tmpBlock));
                                break;
                            case "Int64":
                                np.As<long>().SetValue(newTerminalBlock, p.As<long>().GetValue(tmpBlock));
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
        { }
    }
}

