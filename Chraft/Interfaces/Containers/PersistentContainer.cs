﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Chraft.Net;
using Chraft.Properties;
using Chraft.World;

namespace Chraft.Interfaces.Containers
{
    public abstract class PersistentContainer
    {
        protected string DataPath { get { return Path.Combine(World.Folder, Settings.Default.ContainersFolder); } }
        protected string DataFile;

        protected object _savingLock = new object();
        protected volatile bool _saving;

        protected object _containerLock = new object();

        public WorldManager World;
        public UniversalCoords Coords;
        protected virtual ItemStack[] Slots { get; set; }
        public int SlotsCount;
        public virtual ItemStack this[int slot]
        {
            get
            {
                return Slots[slot];
            }
            protected set
            {
                lock (_containerLock)
                {
                    Slots[slot] = value ?? ItemStack.Void;
                    Slots[slot].Slot = (short) slot;
                }
            }
        }

        public List<PersistentContainerInterface> Interfaces = new List<PersistentContainerInterface>();

        public virtual void Initialize(WorldManager world, UniversalCoords coords)
        {
            World = world;
            Coords = coords;
            Slots = new ItemStack[SlotsCount];
            DataFile = string.Format("x{0}y{1}z{2}.dat", Coords.WorldX, Coords.WorldY, Coords.WorldZ);

            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }
            Load();
        }

        public virtual bool IsEmpty
        {
            get
            {
                bool empty = true;
                foreach (var item in Slots)
                {
                    if (!ItemStack.IsVoid(item))
                    {
                        empty = false;
                        break;
                    }
                }
                return empty;
            }
        }

        #region Save and load

        protected virtual void DoLoad(int slotStart, int slotsCount, string dataFile)
        {
            string file = Path.Combine(DataPath, dataFile);
            if (File.Exists(file))
            {
                using (FileStream containerStream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    using (BigEndianStream bigEndian = new BigEndianStream(containerStream, StreamRole.Server))
                    {
                        for (int i = slotStart; i < slotsCount; i++)
                            Slots[i] = new ItemStack(bigEndian);
                    }
                }
            }
        }

        protected void Load()
        {
            Monitor.Enter(_savingLock);
            try
            {
                DoLoad(0, SlotsCount, DataFile);
                return;
            }
            catch (Exception ex)
            {
                World.Logger.Log(ex);
                return;
            }
            finally
            {
                Monitor.Exit(_savingLock);
            }
        }

        private bool EnterSave()
        {
            lock (_savingLock)
            {
                if (_saving)
                    return false;
                _saving = true;
                return true;
            }
        }

        private void ExitSave()
        {
            _saving = false;
        }

        public void Save()
        {
            if (!EnterSave())
                return;

            try
            {
                DoSave(0, SlotsCount, DataFile);
            }
            catch(Exception ex)
            {
                World.Logger.Log(ex);
            }
            finally
            {
                ExitSave();
            }
        }

        protected virtual void DoSave(int slotStart, int slotsCount, string dataFile)
        {
            string file = Path.Combine(DataPath, dataFile);
            if (IsEmpty)
            {
                File.Delete(file);
                return;
            }
            try
            {
                using (FileStream fileStream = File.Create(file + ".tmp"))
                {
                    using (BigEndianStream bigEndianStream = new BigEndianStream(fileStream, StreamRole.Server))
                    {
                        ItemStack stack = ItemStack.Void;
                        for (int i = slotStart; i < slotsCount; i++)
                        {
                            stack = Slots[i];
                            if (stack != null)
                            {
                                stack.Write(bigEndianStream);
                            }
                            else
                            {
                                ItemStack.Void.Write(bigEndianStream);
                            }
                        }
                    }

                }
            }
            finally
            {
                File.Delete(file);
                File.Move(file + ".tmp", file);
            }
         }

        #endregion

        #region Interface management
        public void AddInterface(PersistentContainerInterface containerInterface)
        {
            lock (_containerLock)
            {
                if (!Interfaces.Contains(containerInterface))
                {
                    containerInterface.Container = this;
                    Interfaces.Add(containerInterface);
                }
            }
        }

        public void RemoveInterface(PersistentContainerInterface containerInterface)
        {
            lock (_containerLock)
            {
                Save();
                Interfaces.Remove(containerInterface);
            }
        }

        public virtual bool IsUnused()
        {
            return !HasInterfaces();
        }

        public bool HasInterfaces()
        {
            return Interfaces.Count > 0;
        }
        #endregion

        public virtual bool SlotCanBeChanged(Net.Packets.WindowClickPacket packet)
        {
            return true;
        }

        public virtual void ChangeSlot(sbyte senderWindowId, short slot, ItemStack newItem)
        {
            Slots[slot] = newItem;
            foreach (var persistentInterface in Interfaces)
                if (persistentInterface.Handle != senderWindowId)
                    persistentInterface[slot] = newItem;
            Save();
        }

        public virtual void Destroy()
        {
            foreach (var persistentInterface in Interfaces)
                persistentInterface.Close(true);
            DropContent();
        }

        public virtual void DropContent()
        {
            lock (_containerLock)
            {
                for (short i = 0; i < Slots.Count(); i++)
                {
                    ItemStack stack = Slots[i];
                    if (!ItemStack.IsVoid(stack))
                    {
                        World.Server.DropItem(World, Coords, stack);
                        this[i] = ItemStack.Void;
                    }
                }
            Save();
            }
        }
    }
}