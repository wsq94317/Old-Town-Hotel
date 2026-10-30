#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.PlayMode
{
    [SetUpFixture]
    public sealed class IsolatedHotelTestSession
    {
        private string _directory;
        [OneTimeSetUp]public void IsolateFromPlayerSaves()
        {
            _directory=Path.Combine(Application.temporaryCachePath,"HotelPlayTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            SaveService.EditorValidationPath=Path.Combine(_directory,"slot.json");SaveSlots.ResetForNewGame();
        }
        [OneTimeTearDown]public void RestorePlayerSaveRouting()
        {
            string path=SaveService.EditorValidationPath;SaveService.EditorValidationPath=null;SaveSlots.ResetForNewGame();
            if(path!=null&&File.Exists(path))File.Delete(path);if(_directory!=null&&Directory.Exists(_directory)&&Directory.GetFileSystemEntries(_directory).Length==0)Directory.Delete(_directory);
        }
    }
}
#endif
