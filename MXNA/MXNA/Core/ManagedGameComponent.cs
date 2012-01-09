using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MXNA.Input;

namespace MXNA
{
    /// <summary>
    /// Enum describes the screen transition state.
    /// </summary>
    public enum ScreenState
    {
        TransitionOn,
        Active,
        TransitionOff,
        Hidden
    }

    /// <summary>
    /// A screen is a single layer that has update and draw logic, and which
    /// can be combined with other layers to build up a complex menu system.
    /// For instance the main menu, the options menu, the "are you sure you
    /// want to quit" message box, and the main game itself are all implemented
    /// as screens.
    /// </summary>
    public abstract class ManagedGameComponent : DrawableGameComponent 
    {
        #region Properties

        public string ID { get; set; }

        /// <summary>
        /// Collection of components this component manages
        /// </summary>
        //public List<ManagedGameComponent> Components
        //{
        //    get { return _Components; }
        //}

        public List<ManagedGameComponent> Components { get; internal set; }
        List<ManagedGameComponent> _ComponentsToUpdate;

        /// <summary>
        /// Gets the manager that this screen belongs to.
        /// </summary>
        public ManagedGameComponent ComponentManager { get; internal set; }

        /// <summary>
        /// Normally when one screen is brought up over the top of another,
        /// the first screen will transition off to make room for the new
        /// one. This property indicates whether the screen is only a small
        /// popup, in which case screens underneath it do not need to bother
        /// transitioning off.
        /// </summary>
        public bool IsPopup { get; set; }


        public bool IsPersistant { get; set; }

        /// <summary>
        /// Indicates how long the screen takes to
        /// transition on when it is activated.
        /// </summary>
        public TimeSpan TransitionOnTime { get; set; }

        /// <summary>
        /// Indicates how long the screen takes to
        /// transition off when it is deactivated.
        /// </summary>
        public TimeSpan TransitionOffTime { get; set; }

        /// <summary>
        /// Gets the current position of the screen transition, ranging
        /// from zero (fully active, no transition) to one (transitioned
        /// fully off to nothing).
        /// </summary>
        public float TransitionPosition { get; protected set; }

        /// <summary>
        /// Gets the current alpha of the screen transition, ranging
        /// from 255 (fully active, no transition) to 0 (transitioned
        /// fully off to nothing).
        /// </summary>
        public byte TransitionAlpha
        {
            get { return (byte)(TransitionPosition * 255); }
        }

        /// <summary>
        /// Gets the current screen transition state.
        /// </summary>
        public ScreenState ScreenState { get; protected set; }

        /// <summary>
        /// There are two possible reasons why a screen might be transitioning
        /// off. It could be temporarily going away to make room for another
        /// screen that is on top of it, or it could be going away for good.
        /// This property indicates whether the screen is exiting for real:
        /// if set, the screen will automatically remove itself as soon as the
        /// transition finishes.
        /// </summary>
        public bool IsExiting { get; protected internal set; }

        /// <summary>
        /// Checks whether this screen is active and can respond to user input.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return !HasFocus &&
                       (ScreenState == ScreenState.TransitionOn ||
                        ScreenState == ScreenState.Active);
            }
        }

        bool HasFocus;

        #endregion

        #region Initialization

        public ManagedGameComponent() : base(G.Game)
        {
            Components = new List<ManagedGameComponent>();
            _ComponentsToUpdate = new List<ManagedGameComponent>();

            TransitionOnTime = TimeSpan.Zero;
            TransitionOffTime = TimeSpan.Zero;
            TransitionPosition = 1;
            ScreenState = ScreenState.TransitionOn;

            IsExiting = false;
            IsPopup = false;
        }

        /// <summary>
        /// Load graphics content for the screen.
        /// </summary>
        public new virtual void LoadContent() { }


        /// <summary>
        /// Unload content for the screen.
        /// </summary>
        public new virtual void UnloadContent() { }


        #endregion

        #region Update and Draw


        /// <summary>
        /// Allows the screen to run logic, such as updating the transition position.
        /// Unlike HandleInput, this method is called regardless of whether the screen
        /// is active, hidden, or in the middle of a transition.
        /// </summary>
        /// 

        public override void Update(GameTime gameTime)
        {

            // Read the keyboard and gamepad.
            //input.Update();

            // Make a copy of the master screen list, to avoid confusion if
            // the process of updating one screen adds or removes others.
            _ComponentsToUpdate.Clear();

            foreach (ManagedGameComponent screen in Components)
            {
                _ComponentsToUpdate.Add(screen);
            }

            bool otherScreenHasFocus = !Game.IsActive;
            bool coveredByOtherScreen = false;

            // Loop as long as there are screens waiting to be updated.
            while (_ComponentsToUpdate.Count > 0)
            {
                // Pop the topmost screen off the waiting list.
                ManagedGameComponent screen = _ComponentsToUpdate[_ComponentsToUpdate.Count - 1];

                _ComponentsToUpdate.RemoveAt(_ComponentsToUpdate.Count - 1);

                // Update the screen.
                screen.Update(gameTime);
                screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

                if (screen.ScreenState == ScreenState.TransitionOn ||
                    screen.ScreenState == ScreenState.Active)
                {
                    // If this is the first active screen we came across,
                    // give it a chance to handle input.
                    if (!otherScreenHasFocus)
                    {
                        //screen.HandleInput(input);

                        otherScreenHasFocus = true;
                    }

                    // If this is an active non-popup, inform any subsequent
                    // screens that they are covered by it.
                    if (!screen.IsPopup)
                        coveredByOtherScreen = true;
                }
            }

        }

        public virtual void Update(GameTime gameTime, bool otherScreenHasFocus,
                                                      bool coveredByOtherScreen)
        {
            this.HasFocus = otherScreenHasFocus;

            if (IsExiting)
            {
                // If the screen is going away to die, it should transition off.
                ScreenState = ScreenState.TransitionOff;

                if (!UpdateTransition(gameTime, TransitionOffTime, 1))
                {
                    // When the transition finishes, remove the screen.
                    if (ComponentManager != null)
                        ComponentManager.Remove(this);
                }
            }
            else if (coveredByOtherScreen && !IsPersistant)
            {
                // If the screen is covered by another, it should transition off.
                if (UpdateTransition(gameTime, TransitionOffTime, 1))
                {
                    // Still busy transitioning.
                    ScreenState = ScreenState.TransitionOff;
                }
                else
                {
                    // Transition finished!
                    ScreenState = ScreenState.Hidden;
                }
            }
            else
            {
                // Otherwise the screen should transition on and become active.
                if (UpdateTransition(gameTime, TransitionOnTime, -1))
                {
                    // Still busy transitioning.
                    ScreenState = ScreenState.TransitionOn;
                }
                else
                {
                    // Transition finished!
                    ScreenState = ScreenState.Active;
                }
            }
        }


        /// <summary>
        /// Helper for updating the screen transition position.
        /// </summary>
        bool UpdateTransition(GameTime gameTime, TimeSpan time, int direction)
        {
            // How much should we move by?
            float transitionDelta;

            if (time == TimeSpan.Zero)
                transitionDelta = 1;
            else
                transitionDelta = (float)(gameTime.ElapsedGameTime.TotalMilliseconds /
                                          time.TotalMilliseconds);

            // Update the transition position.
            TransitionPosition += transitionDelta * direction;

            // Did we reach the end of the transition?
            if (((direction < 0) && (TransitionPosition <= 0)) ||
                ((direction > 0) && (TransitionPosition >= 1)))
            {
                TransitionPosition = MathHelper.Clamp(TransitionPosition, 0, 1);
                return false;
            }

            // Otherwise we are still busy transitioning.
            return true;
        }


        /// <summary>
        /// Allows the screen to handle user input. Unlike Update, this method
        /// is only called when the screen is active, and not when some other
        /// screen has taken the focus.
        /// </summary>
        public virtual void HandleInput(PlayerInput input) { }


        /// <summary>
        /// This is called when the screen should draw itself.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            foreach (ManagedGameComponent screen in Components)
            {
                if (screen.ScreenState == ScreenState.Hidden)
                    continue;

                screen.Draw(gameTime);

            }

        }

        #endregion

        #region Public Methods

        public void Add(ManagedGameComponent component)
        {
            if (component.ComponentManager == null)
            {
                component.ComponentManager = this;
                component.IsExiting = false;
                Components.Add(component);
            }
            else
            {
                //exception here?
                //throw new Exception("Component is already being managed by another component");
            }
        }

        public void Remove(ManagedGameComponent component)
        {
            Components.Remove(component);
            _ComponentsToUpdate.Remove(component);

            component.ComponentManager = null;
        }

        /// <summary>
        /// Tells the screen to go away. Unlike ScreenManager.RemoveScreen, which
        /// instantly kills the screen, this method respects the transition timings
        /// and will give the screen a chance to gradually transition off.
        /// </summary>
        public void ExitScreen()
        {
            if (TransitionOffTime == TimeSpan.Zero)
            {
                // If the screen has a zero transition time, remove it immediately.
                ComponentManager.Remove(this);
            }
            else
            {
                // Otherwise flag that it should transition off and then exit.
                IsExiting = true;
            }
        }

        public static void FadeBackBufferToBlack(int alpha)
        {
            Viewport viewport = G.Game.GraphicsDevice.Viewport;
            Texture2D blankTexture = G.Content.Load<Texture2D>("blank");

            G.SpriteBatch.Draw(blankTexture,
                             new Rectangle(0, 0, viewport.Width, viewport.Height),
                             new Color(255, 255, 255, (byte)alpha));
        }
        #endregion

        #region DEBUG

        public void DrawDebugInfo(int x, int y)
        {
            int i = 12;
            foreach (ManagedGameComponent gc in Components )
            {
                i += i;
                G.SpriteBatch.DrawString(Game.Content.Load<SpriteFont>(@"SpriteFonts\Arial"), gc.ID + " " + gc.ScreenState.ToString() , new Vector2(x, y + i), Color.Red);

                gc.DrawDebugInfo(x + 40, y + 10);
            }
                 
        }
  
        #endregion
    }
}
