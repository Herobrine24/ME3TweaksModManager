﻿using System.ComponentModel;
using System.Windows;
using IniParser.Model;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for Game1TLKMergeEditorControl.xaml
    /// </summary>
    public partial class Game1TLKMergeEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public Game1TLKMergeEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }


        public bool UsesFeature { get; set; }

        private void LoadCommands()
        {

        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            var job = EditingMod?.GetJob(ModJob.JobHeader.GAME1_EMBEDDED_TLK);
            UsesFeature = job != null;
            HasLoaded = true;
        }


        public override void Serialize(IniData ini)
        {
            if (UsesFeature)
            {
                ini[ModJob.JobHeader.GAME1_EMBEDDED_TLK.ToString()][@"usesfeature"] = @"true";
            }
        }
    }
}
