using System;
using BoBox.Bridge;

namespace BoBox.Game
{
    /// <summary>
    /// Fires a god power at a tile by invoking the same public delegates that the game's own
    /// PlayerControl.clickedFinal invokes (decompiled/PlayerControl.cs, clickedFinal), in the same order.
    /// clickedFinal itself is internal, so it cannot be called directly.
    /// Powers that read PlayerControl.first_pressed_* (internal press state) may throw midway; the caller reports a 422.
    /// </summary>
    internal static class PowerDispatcher
    {
        public static bool IsFireable(GodPower power)
        {
            return power.click_special_action != null
                || power.click_power_action != null
                || power.click_power_brush_action != null
                || power.click_action != null
                || power.click_brush_action != null;
        }

        public static void Fire(GodPower power, WorldTile tile)
        {
            if (!IsFireable(power))
            {
                throw new BridgeException(400, "power has no click action (toggle or UI-only): " + power.id);
            }
            if (power.requires_premium && !Config.hasPremium)
            {
                throw new BridgeException(403, "power requires premium: " + power.id);
            }

            try
            {
                Invoke(power, tile);
            }
            catch (Exception ex)
            {
                Log.Error("power " + power.id + " failed mid-action", ex);
                throw new BridgeException(422, "power failed mid-action: " + power.id);
            }
        }

        private static void Invoke(GodPower power, WorldTile tile)
        {
            if (power.click_special_action != null)
            {
                power.click_special_action(tile, power.id);
            }
            else if (power.click_power_brush_action != null)
            {
                power.click_power_brush_action(tile, power);
            }
            else if (power.click_power_action != null)
            {
                power.click_power_action(tile, power);
            }
            else if (power.click_brush_action != null)
            {
                power.click_brush_action(tile, power.id);
            }
            else
            {
                power.click_action(tile, power.id);
            }
        }
    }
}
