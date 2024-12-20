using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using System.IO;
using System.Collections.Generic;

namespace MediumcoreGhostInventories.NPCs
{
    public class GhostInventory : ModNPC
    {
        public PlayerDeathInventory storedInventory;
        private Point position;
        private bool attemptedToStoreInventory = false;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Ghost");
            Main.npcFrameCount[NPC.type] = 4;
        }

        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 24;
            NPC.lifeMax = 100;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.friendly = true;
            NPC.npcSlots = 0;
            NPC.aiStyle = -1;
            //npc.netAlways = true;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.alpha = 50;
            NPC.velocity = Vector2.Zero;
            NPC.dontTakeDamageFromHostiles = true;
            NPC.dontCountMe = true;
        }

        //Make sure does not despawn
        public override bool CheckActive() => false;

        public override bool CanChat() => true;
        
        public override void AI()
        {
            Lighting.AddLight(NPC.position, 0.25F, 0.25F, 0.25F);

            //Get the inventory and corresponding player death position to be restored
            //The player death position is passed through the ai0 and ai1 parameters on the NewNPC method.
            // ai0 = deathPosition.X 
            // ai1 = deathPosition.Y
            if (!attemptedToStoreInventory)
            {
                position = new Point((int)NPC.ai[0], (int)NPC.ai[1]);
                NPC.position = new Vector2(NPC.ai[0], NPC.ai[1]);
                Mod.Logger.Debug($"ghost position - {position}");

                if (ModContent.GetInstance<MediumcoreGhostInventoriesWorld>().playerDeathInventoryMap.ContainsKey(position))
                    storedInventory = ModContent.GetInstance<MediumcoreGhostInventoriesWorld>().playerDeathInventoryMap[position];

                attemptedToStoreInventory = true;
            }
            //If inventory was attempted to be loaded and the inventory dict no longer contains the corresponding inventory, then kill this npc
            else if (!ModContent.GetInstance<MediumcoreGhostInventoriesWorld>().playerDeathInventoryMap.ContainsKey(position))
            {
                Mod.Logger.Debug($"Killing ghost: no inventory - {position}");
                NPC.active = false;
            }
        }

        public override string GetChat()
        {
            if (Main.player[Main.myPlayer].name == storedInventory.playerName)
            {
                return $"Hi {storedInventory.playerName} good to see you again...";
            }
            else
                return $"I am the ghost of {storedInventory.playerName}";
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            if (Main.player[Main.myPlayer].name == storedInventory.playerName)
            {
                button = $"Retrieve {storedInventory.numOfItems} items";
            }
            button2 = "Drop all items";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            //First button = give inventory
            if (firstButton)
            {
                Player player = Main.player[Main.myPlayer];
                player.DropItems();
                GivePlayerInventoryBack(player);
            }
            //Second button = drop stored inventory
            else
            {
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    SendDropInventory();
                else  
                    storedInventory.DropInventory(NPC.getRect());
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                SendKill();

            Mod.Logger.Debug($"Killing ghost: button clicked - {position}");
            NPC.active = false;
            ModContent.GetInstance<MediumcoreGhostInventoriesWorld>().playerDeathInventoryMap.Remove(position);
        }

        //Send packet to the server to tell it to drop this npcs corresponding inventory on the ground
        private void SendDropInventory()
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)MediumcoreGhostInventories.MediumcoreGhostInventoriesMessageType.DropInventory);

            packet.Write(position.X);
            packet.Write(position.Y);

            Rectangle rect = NPC.getRect();
            packet.Write(rect.X);
            packet.Write(rect.Y);
            packet.Write(rect.Height);
            packet.Write(rect.Width);

            packet.Send();
        }

        //Send packet to server to tell it to kill this npc and remove its corresponding inventory from the world
        private void SendKill()
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)MediumcoreGhostInventories.MediumcoreGhostInventoriesMessageType.KillNPC);

            packet.Write(position.X);
            packet.Write(position.Y);
            packet.Send();
        }

        private void GivePlayerInventoryBack(Player player)
        {
            //Update stored inventory just incase it has changed (for example which items were favourited was received late)
            storedInventory = ModContent.GetInstance<MediumcoreGhostInventoriesWorld>().playerDeathInventoryMap[position];

            player.inventory = storedInventory.deathInventory;
            player.armor = storedInventory.deathArmor;
            player.dye = storedInventory.deathDye;
            player.miscEquips = storedInventory.deathMiscEquips;
            player.miscDyes = storedInventory.deathMiscDyes;
        }

        public override void FindFrame(int frameHeight)
        {
            //every 12th frame chose next sprite frame
            if (NPC.frameCounter++ >= 12)
            {
                NPC.frameCounter = 0;
                if ((NPC.frame.Y += frameHeight) >= 4 * frameHeight)
                {
                    NPC.frame.Y = 0;
                }
            }
        }
    }
}
