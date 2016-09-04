﻿using System.Collections;
using System.Windows;
using WoWEditor6.UI.Dialogs;
using System.Windows.Forms;
using SharpDX;
using WoWEditor6.Editing;
using WoWEditor6.UI.Widget;

namespace WoWEditor6.UI.Models
{
    class IEditingViewModel
    {
        private readonly IEditingWidget mWidget;

        public IEditingWidget Widget { get { return mWidget; } }

        public IEditingViewModel(IEditingWidget widget)
        {
            if (EditorWindowController.Instance != null)
                EditorWindowController.Instance.IEditingModel = this;

            mWidget = widget;
        }

        public void SwitchWidgets(int widget)
        {
            switch (widget)
            {
                case 0:
                {
                    mWidget.TexturingWidget.Visibility = Visibility.Hidden;
                    mWidget.TerrainSettingsWidget.Visibility = Visibility.Hidden;
                    mWidget.ShadingWidget.Visibility = Visibility.Hidden;
                    mWidget.ModelSpawnWidget.Visibility = Visibility.Hidden;
                    break;
                }

                case 1:
                {
                    mWidget.TexturingWidget.Visibility = Visibility.Hidden;
                    mWidget.TerrainSettingsWidget.Visibility = Visibility.Visible;
                    mWidget.ShadingWidget.Visibility = Visibility.Hidden;
                    mWidget.ModelSpawnWidget.Visibility = Visibility.Hidden;
                    EditManager.Instance.EnableSculpting();
                    break;
                }

                case 3:
                {
                    mWidget.TexturingWidget.Visibility = Visibility.Visible;
                    mWidget.TerrainSettingsWidget.Visibility = Visibility.Hidden;
                    mWidget.ShadingWidget.Visibility = Visibility.Hidden;
                    mWidget.ModelSpawnWidget.Visibility = Visibility.Hidden;
                    EditManager.Instance.EnableTexturing();
                    break;
                }

                case 4:
                {
                    mWidget.TexturingWidget.Visibility = Visibility.Hidden;
                    mWidget.TerrainSettingsWidget.Visibility = Visibility.Hidden;
                    mWidget.ModelSpawnWidget.Visibility = Visibility.Hidden;
                    mWidget.ShadingWidget.Visibility = Visibility.Visible;
                    EditManager.Instance.EnableSculpting();
                    EditManager.Instance.EnableShading();
                    break;
                }

                case 5:
                {
                    mWidget.TexturingWidget.Visibility = Visibility.Hidden;
                    mWidget.TerrainSettingsWidget.Visibility = Visibility.Hidden;
                    mWidget.ShadingWidget.Visibility = Visibility.Hidden;
                    mWidget.ModelSpawnWidget.Visibility = Visibility.Visible;
                    break;
                }
            }

        }


    }
}
