﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Chraft.World;

namespace Chraft.Entity.Mobs
{
    public class Pig : Mob
    {
        public override string Name
        {
            get { return "Pig"; }
        }

        internal Pig(Chraft.World.WorldManager world, int entityId, Chraft.Net.MetaData data = null)
            : base(world, entityId, MobType.Pig, data)
        {
        }

        protected override void DoDeath(EntityBase killedBy)
        {
            sbyte count = (sbyte)Server.Rand.Next(2);
            if (count > 0)
                Server.DropItem(World, UniversalCoords.FromWorld(Position.X, Position.Y, Position.Z), new Interfaces.ItemStack((short)Chraft.World.BlockData.Items.Pork, count, 0));
            // TODO: if death by fire drop cooked pork
        }
    }
}
