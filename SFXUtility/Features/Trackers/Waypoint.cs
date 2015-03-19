﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Waypoint.cs is part of SFXUtility.

 SFXUtility is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXUtility is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXUtility. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

namespace SFXUtility.Features.Trackers
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Classes;
    using LeagueSharp;
    using LeagueSharp.Common;
    using SFXLibrary;
    using SFXLibrary.Extensions.SharpDX;
    using SFXLibrary.IoCContainer;
    using SFXLibrary.Logger;
    using SharpDX;
    using Color = System.Drawing.Color;

    #endregion

    internal class Waypoint : Base
    {
        private const float UpdateInterval = 300f;
        private float _lastUpdate;
        private Trackers _parent;
        private IEnumerable<List<Vector2>> _waypoints = new List<List<Vector2>>();

        public Waypoint(IContainer container)
            : base(container)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        public override bool Enabled
        {
            get
            {
                return _parent != null && _parent.Enabled && Menu != null &&
                       Menu.Item(Name + "Enabled").GetValue<bool>();
            }
        }

        public override string Name
        {
            get { return "Waypoint"; }
        }

        protected override void OnEnable()
        {
            Game.OnUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDrawingDraw;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Game.OnUpdate -= OnGameUpdate;
            Drawing.OnDraw -= OnDrawingDraw;
            base.OnDisable();
        }

        private void OnGameUpdate(EventArgs args)
        {
            try
            {
                if (_lastUpdate + UpdateInterval > Environment.TickCount)
                    return;

                _lastUpdate = Environment.TickCount;

                _waypoints = (HeroManager.AllHeroes.Where(hero => hero.IsValid && !hero.IsDead)
                    .Where(hero => hero.IsAlly && Menu.Item(Name + "DrawAlly").GetValue<bool>())
                    .Where(hero => hero.IsEnemy && Menu.Item(Name + "DrawEnemy").GetValue<bool>())).Select(
                        hero => hero.GetWaypoints());
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                var crossColor = Menu.Item(Name + "DrawingCrossColor").GetValue<Color>();
                var lineColor = Menu.Item(Name + "DrawingLineColor").GetValue<Color>();

                foreach (var waypoints in _waypoints)
                {
                    var arrivalTime = 0.0f;
                    for (int i = 0, l = waypoints.Count - 1; i < l; i++)
                    {
                        if (!waypoints[i].IsValid() || !waypoints[i + 1].IsValid())
                            continue;

                        var current = Drawing.WorldToScreen(waypoints[i].To3D());
                        var next = Drawing.WorldToScreen(waypoints[i + 1].To3D());

                        arrivalTime += (Vector3.Distance(waypoints[i].To3D(), waypoints[i + 1].To3D())/
                                        (ObjectManager.Player.MoveSpeed/1000))/1000;

                        if (current.IsOnScreen(next))
                        {
                            Drawing.DrawLine(current.X, current.Y, next.X, next.Y, 1, lineColor);
                            if (i == l - 1 && arrivalTime > 0.1f)
                            {
                                Draw.Cross(next, 10f, 2f, crossColor);
                                Draw.TextCentered(new Vector2(next.X - 5, next.Y + 15), crossColor,
                                    arrivalTime.ToString("0.0"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private void OnGameLoad(EventArgs args)
        {
            try
            {
                if (IoC.IsRegistered<Trackers>())
                {
                    _parent = IoC.Resolve<Trackers>();
                    if (_parent.Initialized)
                        OnParentLoaded(null, null);
                    else
                        _parent.OnInitialized += OnParentLoaded;
                }
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private void OnParentLoaded(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                    return;

                Menu = new Menu(Name, Name);

                var drawingMenu = new Menu("Drawing", Name + "Drawing");
                drawingMenu.AddItem(new MenuItem(Name + "DrawingCrossColor", "Cross Color").SetValue(Color.DarkRed));
                drawingMenu.AddItem(new MenuItem(Name + "DrawingLineColor", "Line Color").SetValue(Color.White));

                Menu.AddSubMenu(drawingMenu);

                Menu.AddItem(new MenuItem(Name + "DrawAlly", "Ally").SetValue(false));
                Menu.AddItem(new MenuItem(Name + "DrawEnemy", "Enemy").SetValue(true));
                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(false));

                _parent.Menu.AddSubMenu(Menu);

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }
    }
}