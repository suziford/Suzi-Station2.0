// SPDX-FileCopyrightText: 2020 R. Neuser <rneuser@iastate.edu>
// SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2021 Acruid <shatter66@gmail.com>
// SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 GraniteSidewalk <32942106+GraniteSidewalk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 deathride58 <deathride58@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffect;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Flash
{
    public sealed class FlashOverlay : Overlay
    {
        private static readonly ProtoId<ShaderPrototype> FlashedEffectShader = "FlashedEffect";
        private static readonly ProtoId<ShaderPrototype> CircleShader = "CircleMask";

        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IConfigurationManager _configManager = default!;


        private readonly SharedFlashSystem _flash;
        private readonly StatusEffectsSystem _statusSys;
        private readonly ShaderInstance _circleMaskShader;

        public override OverlaySpace Space => OverlaySpace.WorldSpace;
        private readonly ShaderInstance _shader;
        private bool _reducedMotion;
        public float PercentComplete;
        public Texture? ScreenshotTexture;

        private const float NoMotion_Radius = 30.0f; // Base radius for the nomotion variant at its full strength
        private const float NoMotion_Pow = 0.2f; // Exponent for the nomotion variant's gradient
        private const float NoMotion_Max = 8.0f; // Max value for the nomotion variant's gradient
        private const float NoMotion_Mult = 0.75f; // Multiplier for the nomotion variant

        public FlashOverlay()
        {
            IoCManager.InjectDependencies(this);
            _shader = _prototypeManager.Index(FlashedEffectShader).InstanceUnique();
            _flash = _entityManager.System<SharedFlashSystem>();
            _statusSys = _entityManager.System<StatusEffectsSystem>();

            _configManager.OnValueChanged(CCVars.ReducedMotion, (b) => { _reducedMotion = b; }, invokeImmediately: true);

            _circleMaskShader = _prototypeManager.Index(CircleShader).InstanceUnique();

            _circleMaskShader.SetParameter("CircleMinDist", 0.0f);
            _circleMaskShader.SetParameter("CirclePow", NoMotion_Pow);
            _circleMaskShader.SetParameter("CircleMax", NoMotion_Max);
            _circleMaskShader.SetParameter("CircleMult", NoMotion_Mult);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            var playerEntity = _playerManager.LocalEntity;

            if (playerEntity == null)
                return;

            if (!_entityManager.HasComponent<FlashedComponent>(playerEntity)
                || !_entityManager.TryGetComponent<StatusEffectsComponent>(playerEntity, out var status))
                return;

            if (!_statusSys.TryGetTime(playerEntity.Value, _flash.FlashedKey, out var time, status))
                return;

            var curTime = _timing.CurTime;
            var lastsFor = (float)(time.Value.Item2 - time.Value.Item1).TotalSeconds;
            var timeDone = (float)(curTime - time.Value.Item1).TotalSeconds;

            PercentComplete = timeDone / lastsFor;
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
                return false;
            if (args.Viewport.Eye != eyeComp.Eye)
                return false;

            return PercentComplete < 1.0f;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (RequestScreenTexture && ScreenTexture != null)
            {
                ScreenshotTexture = ScreenTexture;
                RequestScreenTexture = false; // we only need the first frame, so we can stop the request now for performance reasons
            }
            if (ScreenshotTexture == null)
                return;

            var worldHandle = args.WorldHandle;
            if (_reducedMotion)
            {
                if (ScreenTexture == null)
                    return;
                // TODO: This is a very simple placeholder.
                // Replace it with a proper shader once we come up with something good.
                // Turns out making an effect that is supposed to be a bright, sudden, and disorienting flash
                // not do any of that while also being equivalent in terms of game balance is hard.
                //vds/imp
                var alpha = 1 - MathF.Pow(PercentComplete, 15f);
                var vignetteIntensity = 1 - MathF.Pow((2*PercentComplete)-1, 2f);

                worldHandle.DrawTextureRectRegion(ScreenshotTexture, args.WorldBounds, new Color(1f, 1f, 1f, alpha));
                _circleMaskShader.SetParameter("Zoom", 1f);
                _circleMaskShader.SetParameter("CircleRadius", NoMotion_Radius / vignetteIntensity);

                worldHandle.UseShader(_circleMaskShader);
                worldHandle.DrawRect(args.WorldBounds, Color.White);
                worldHandle.UseShader(null);
                //vds/imp
                return;
            }
            else
            {
                // VDS
                var alpha = 1 - MathF.Pow(PercentComplete, 8f) - 0.2f;
                var alphashade = 1 - MathF.Pow(PercentComplete, 10f) - 0.1f;
                worldHandle.DrawTextureRectRegion(ScreenshotTexture, args.WorldBounds, new Color(1f, 1f, 1f, alpha));
                worldHandle.DrawTextureRectRegion(ScreenshotTexture, args.WorldBounds, new Color(0f, 0f, 0f, alphashade));
                //VDS end
            }
        }

        protected override void DisposeBehavior()
        {
            base.DisposeBehavior();
            ScreenshotTexture = null;
        }
    }
}