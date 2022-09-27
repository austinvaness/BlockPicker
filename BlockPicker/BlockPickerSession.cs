using HarmonyLib;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Gui;
using System.Reflection;
using VRage.Game.Components;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using Sandbox.Game.World;
using Sandbox;

namespace avaness.BlockPicker
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class BlockPickerSession : MySessionComponentBase
    {
        private readonly FieldInfo f_m_gizmo;
        private readonly FieldInfo f_m_currentGrid;
        private Modes mode = Modes.BLOCK_COLOR;

        public BlockPickerSession()
        {
            f_m_gizmo = AccessTools.Field(typeof(MyCubeBuilder), "m_gizmo");
            f_m_currentGrid = AccessTools.Field(typeof(MyCubeBuilder), "m_currentGrid");
        }

        public override void HandleInput()
        {
            if (!MySandboxGame.IsPaused && MySession.Static.LocalHumanPlayer != null && MyCubeBuilder.Static != null && !MyGuiScreenGamePlay.DisableInput && IsKeybindPressed() 
                && MyCubeBuilder.Static.IsBuildToolActive() && CanBuild())
            {

                if (MyInput.Static.IsAnyShiftKeyPressed())
                {
                    SwitchMode();
                    return;
                }

                MyCubeBuilderGizmo gizmo = (MyCubeBuilderGizmo)f_m_gizmo.GetValue(MyCubeBuilder.Static);
                MyCubeGrid currentGrid = (MyCubeGrid)f_m_currentGrid.GetValue(MyCubeBuilder.Static);
                if (gizmo == null || currentGrid == null)
                    return;

                foreach (MyCubeBuilderGizmo.MyGizmoSpaceProperties property in gizmo.Spaces)
                {
                    if (property.m_removePos == null)
                        continue;

                    MySlimBlock cubeBlock = currentGrid.GetCubeBlock(property.m_removePos);
                    if (cubeBlock != null)
                    {
                        UpdateGizmo(gizmo, cubeBlock);
                        return;
                    }
                }
            }
        }

        private void UpdateGizmo(MyCubeBuilderGizmo gizmo, MySlimBlock cubeBlock)
        {
            // When the primary GUI block of the selected block's variant group
            // is set before the selected block, the user can scroll between variants.
            var guiVariant = cubeBlock.BlockDefinition.BlockVariantsGroup.PrimaryGUIBlock;
            if (guiVariant != null)
                MyCubeBuilder.Static.CubeBuilderState.UpdateCubeBlockDefinition(guiVariant.Id, gizmo.SpaceDefault.m_localMatrixAdd);

            MyCubeBuilder.Static.CubeBuilderState.UpdateCubeBlockDefinition(cubeBlock.BlockDefinition.Id, gizmo.SpaceDefault.m_localMatrixAdd);

            cubeBlock.Orientation.GetQuaternion(out Quaternion quat);
            gizmo.SpaceDefault.m_localMatrixAdd = Matrix.CreateFromQuaternion(quat);
            if (mode == Modes.BLOCK_COLOR)
            {
                MySession.Static.LocalHumanPlayer.ChangeOrSwitchToColor(cubeBlock.ColorMaskHSV);
                MyStringHash skinSubtypeId = cubeBlock.SkinSubtypeId;
                MySession.Static.LocalHumanPlayer.BuildArmorSkin = (skinSubtypeId == MyStringHash.NullOrEmpty ? string.Empty : skinSubtypeId.ToString());
            }
        }

        private bool IsKeybindPressed()
        {
            return MyInput.Static.IsNewGameControlPressed(MyControlsSpace.BUILD_SCREEN) && MyInput.Static.IsAnyCtrlKeyPressed();
        }

        private bool CanBuild()
        {
            return MyBlockBuilderBase.SpectatorIsBuilding || !(MySession.Static.ControlledEntity is MyCockpit cockpit) || cockpit.BuildingMode;
        }

        private void SwitchMode()
        {
            mode++;
            if (mode > Modes.BLOCK_COLOR)
                mode = Modes.BLOCK;

            string s = "";
            switch (mode)
            {
                case Modes.BLOCK:
                    s = "Block only";
                    break;
                case Modes.BLOCK_COLOR:
                    s = "Block & Color";
                    break;
            }

            MyHud.Chat.ShowMessage("[BlockPicker]", "Block picker mode set to: " + s);
        }

    }
}