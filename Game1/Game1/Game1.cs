﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System;

namespace Game1
{
    public static class Assets
    {   //simulation fields
        public static float gravity = 5.0f;
        public static float vertletMulti = 0.5f;
        public static float friction = 0.98f;
        public static float massOfPoints = 0.7f;
        public static float stiffnesses = 0.9f;
        public static byte relaxIterations = 3;

        public static byte curtainWidth = 141;
        public static byte curtainHeight = 23;
        public static byte pinNthTopPointMass = 5;
        public static byte spacing = 8; //how far apart points are created
        public static float restingDistances = 7; 
        //program assets
        public static GraphicsDeviceManager graphics;
        public static GraphicsDevice graphicsDevice;
        public static SpriteBatch spriteBatch;
        public static Texture2D lineTexture;
        public static int gameWindowWidth = 1280;
        public static int gameWindowHeight = 720;
        public static void Setup()
        {
            lineTexture = new Texture2D(graphicsDevice, 1, 1);
            lineTexture.SetData<Color>(new Color[] { Color.White });
        }
        //timing/testing assets
        public static Stopwatch updateTimer = new Stopwatch();
        public static Stopwatch drawTimer = new Stopwatch();
    }


    #region Data

    public class Line
    {
        public PointMass P1;
        public PointMass P2;
        public bool visible = true;
        public float angle = 0.0f;
        public int length = 0;
        public Rectangle rec = new Rectangle(0, 0, 1, 1); //this is drawn
        public Texture2D texture = Assets.lineTexture; //never changes
        public Rectangle texRec = new Rectangle(0, 0, 1, 1); //tex rec
        public Vector2 texOrigin = new Vector2(0, 0);
        public Line(PointMass p1, PointMass p2) { P1 = p1; P2 = p2; }
    }

    public class PointMass
    {
        public float X, Y;
        public float lastX, lastY;
        public float accX, accY = 0;
        public bool pinned = false;
        public PointMass[] neighbors = new PointMass[4];
        
        public PointMass(int xPos, int yPos) { X = xPos; Y = yPos; }
    }

    //migrating pointmass class to array structs in static pool instance
    public static class PM_ArrayPool
    {
        public static int size = 4096;
        public static bool[] active = new bool[size];
        public static float[] posX = new float[size];
        public static float[] posY = new float[size];
        public static float[] accX = new float[size];
        public static float[] accY = new float[size];
        public static bool[] pinned = new bool[size];
        //neighbor indices of pointMass
        public static int[] neighbor_up = new int[size];
        public static int[] neighbor_right = new int[size];
        public static int[] neighbor_down = new int[size];
        public static int[] neighbor_left = new int[size];

        public static int PlacePM(float X, float Y)
        {
            for(int i = 0; i < size; i++)
            {
                if (active[i] == false)
                {
                    active[i] = true;
                    posX[i] = X; posY[i] = Y;
                    return i;
                }
            }
            return size;
        }
    }











    public enum MouseButtons { LeftButton, RightButton }

    public static class InputData
    {
        public static KeyboardState currentKeyboardState = new KeyboardState();
        public static MouseState currentMouseState = new MouseState();
        public static KeyboardState lastKeyboardState = new KeyboardState();
        public static MouseState lastMouseState = new MouseState();
        public static void Update()
        {   //store the last input state
            lastKeyboardState = currentKeyboardState;
            lastMouseState = currentMouseState;
            //get the current input states + cursor position
            currentKeyboardState = Keyboard.GetState();
            currentMouseState = Mouse.GetState();
        }
        public static bool IsLeftMouseBtnPress()
        {   //check to see if L mouse button was pressed
            return (currentMouseState.LeftButton == ButtonState.Pressed &&
                    lastMouseState.LeftButton == ButtonState.Released);
        }
        public static bool IsRightMouseBtnPress()
        {   //check to see if L mouse button was pressed
            return (currentMouseState.RightButton == ButtonState.Pressed &&
                    lastMouseState.RightButton == ButtonState.Released);
        }
    }

    #endregion


    #region Functionality

    public static class Functions_Simulate
    {
        public static void Constrain(PointMass Pm)
        {
            if (Pm.pinned) { return; }

            //keep the PointMasss within the screen
            if (Pm.Y < 1)
            { Pm.Y = 2 * (1) - Pm.Y; }
            else if (Pm.Y > Assets.gameWindowHeight - 1)
            { Pm.Y = 2 * (Assets.gameWindowHeight - 1) - Pm.Y; }

            if (Pm.X > Assets.gameWindowWidth - 1)
            { Pm.X = 2 * (Assets.gameWindowWidth - 1) - Pm.X; }
            else if (Pm.X < 1)
            { Pm.X = 2 * (1) - Pm.X; }
        }

        public static void RelaxPM(PointMass Pm)
        {
            for (int x = 0; x < Assets.relaxIterations; x++)
            {
                for (int g = 0; g < 4; g++)
                {
                    if (Pm.neighbors[g] != null)
                    {
                        //calculate the distance between the two PointMassses
                        float diffX = Pm.X - Pm.neighbors[g].X;
                        float diffY = Pm.Y - Pm.neighbors[g].Y;
                        float d = (float)Math.Sqrt(diffX * diffX + diffY * diffY);

                        //find the difference, or the ratio of how far along the restingDistance the actual distance is.
                        float difference = (Assets.restingDistances - d) / d;

                        //define a coefficient that determines how attractive PMs are to each other
                        float coef = 0.2f;

                        //push/pull based on mass
                        if (Pm.pinned == false)
                        {
                            Pm.X += diffX * difference * coef;
                            Pm.Y += diffY * difference * coef;
                        }
                        if (Pm.neighbors[g].pinned == false)
                        {
                            Pm.neighbors[g].X -= diffX * difference * coef;
                            Pm.neighbors[g].Y -= diffY * difference * coef;
                        }
                    }
                }
            }
        }

        public static void ApplyPhysics(PointMass Pm)
        {
            if (Pm.pinned) { return; }
            float nextX, nextY;
            float velocityX, velocityY;

            //calc velocity
            velocityX = Pm.X - Pm.lastX;
            velocityY = Pm.Y - Pm.lastY;
            
            //dampen velocity
            velocityX *= Assets.friction;
            velocityY *= Assets.friction;
            //clip velocity
            if (Math.Abs(velocityX) < 0.04f) { velocityX = 0.0f; }
            if (Math.Abs(velocityY) < 0.04f) { velocityY = 0.0f; }
            
            //calculate the next position using Verlet Integration
            nextX = (Pm.X + velocityX + Assets.vertletMulti * Pm.accX);
            nextY = (Pm.Y + velocityY + Assets.vertletMulti * (Pm.accY + Assets.massOfPoints * Assets.gravity));
            //reset variables
            Pm.lastX = Pm.X;
            Pm.lastY = Pm.Y;
            Pm.X = nextX;
            Pm.Y = nextY;
            Pm.accX = 0;
            Pm.accY = 0;
        }

        public static void UpdateLine(Line Line)
        {   //set angle
            Line.angle = (float)Math.Atan2(
                (Line.P1.Y - Line.P2.Y),
                (Line.P1.X - Line.P2.X));
            //set length
            Line.length = (int)Vector2.Distance(
                new Vector2(Line.P2.X, Line.P2.Y),
                new Vector2(Line.P1.X, Line.P1.Y));
            //update the rectangle parameters based on length, angle
            Line.rec.X = (int)Line.P2.X;
            Line.rec.Y = (int)Line.P2.Y;
            Line.rec.Width = Line.length;
            Line.rec.Height = 1;
        }

    }

    public static class Functions_Draw
    {
        public static void Draw(Line Line)
        {
            Assets.spriteBatch.Draw(
                Line.texture,
                Line.rec, //draw rec
                Line.texRec, //texture rec
                Color.MonoGameOrange, //at 100% alpha
                Line.angle,
                Line.texOrigin, //wat is this Vector2 origin parameter?
                SpriteEffects.None,
                0.000001f //'layer'
            );
        }
    }

    #endregion


    #region Game (update + draw)

    public class Game1 : Game
    {   //curtain position
        public static Point curPos = new Point(16, 16);
        //10k tix = 1ms, 16ms total = 160,000 total pixels
        //reduce by x1000 = 160 pixels. then x8 = 1280 max size width (minus offset for looks)
        public Rectangle maxTimeRec = new Rectangle(curPos.X, curPos.Y, 1280-31, 6);
        //timer recs for update and draw
        public Rectangle updateTimeRec = new Rectangle(curPos.X, curPos.Y, 100, 3);
        public Rectangle drawTimeRec = new Rectangle(curPos.X, curPos.Y+3, 100, 3);
        
        //cursor rec hitbox
        public Rectangle cursorRec = new Rectangle(0, 0, 34, 34);
        List<PointMass> PointMasses;
        //PointMass grabbedPM;
        List<Line> Lines;
        int i;

        public Game1()
        {
            Assets.graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            base.Initialize();
            this.IsMouseVisible = true;
            Assets.graphics.PreferredBackBufferWidth = Assets.gameWindowWidth;
            Assets.graphics.PreferredBackBufferHeight = Assets.gameWindowHeight;
            Assets.graphics.ApplyChanges();
        }

        protected override void LoadContent()
        {
            Assets.graphicsDevice = GraphicsDevice;
            Assets.spriteBatch = new SpriteBatch(GraphicsDevice);
            Assets.Setup();
            
            PointMasses = new List<PointMass>();
            Lines = new List<Line>();
            int counter;


            #region create the point masses
            
            for (int x = 0; x < Assets.curtainWidth; x++)
            {
                for (int y = 0; y < Assets.curtainHeight; y++)
                {   //spread pms out
                    PointMass pm = new PointMass(curPos.X + 64 + x * Assets.spacing, curPos.Y + y * Assets.spacing);
                    if (y == 0)
                    {
                        //pm.pinned = true; //pin entire top row
                        //pin some of top row curtain
                        if (x == 0 || x == Assets.curtainWidth - 1) { pm.pinned = true; }
                        if (x % Assets.pinNthTopPointMass == 0) { pm.pinned = true; }
                    }
                    PointMasses.Add(pm); //ad pm to game list
                }
            }

            #endregion


            #region setup point masses neighbors

            counter = 0;
            //int x = 0; x < Assets.curtainWidth; x++
            for (int x = 0; x < Assets.curtainWidth; x++)
            {
                for (int y = 0; y < Assets.curtainHeight; y++)
                {

                    if (y < (Assets.curtainHeight - 1)) //connect top to bottom, and vice versa
                    {
                        PointMasses[counter].neighbors[2] = PointMasses[counter + 1];
                        PointMasses[counter + 1].neighbors[0] = PointMasses[counter];
                    }
                    if (x < (Assets.curtainWidth - 1)) //connect left to right point, and vice versa
                    {   //neighbors ref ////0=up, 1=right, 2=down, 3=left
                        PointMasses[counter].neighbors[1] = PointMasses[counter + Assets.curtainHeight];
                        PointMasses[counter + Assets.curtainHeight].neighbors[3] = PointMasses[counter];
                    }
                    
                    //track which pm index we are at
                    counter++;
                }
            }



            #endregion


            #region assign point masses to lines

            counter = 0;
            for (int x = 0; x < Assets.curtainWidth; x++)
            {
                for (int y = 0; y < Assets.curtainHeight; y++)
                {
                    if (x < (Assets.curtainWidth - 1)) //connect left to right point
                    { Lines.Add(new Line(PointMasses[counter], PointMasses[counter + Assets.curtainHeight])); }
                    if (y < (Assets.curtainHeight - 1)) //connect top to bottom
                    { Lines.Add(new Line(PointMasses[counter], PointMasses[counter + 1])); }
                    
                    //track which pm index we are at
                    counter++;
                }
            }

            #endregion

        }

        protected override void UnloadContent() { }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            Assets.updateTimer.Restart();


            #region Process Curtain Simulation

            //update point masses
            for (i = 0; i < PointMasses.Count; i++)
            {
                Functions_Simulate.Constrain(PointMasses[i]);
                Functions_Simulate.ApplyPhysics(PointMasses[i]);
                Functions_Simulate.RelaxPM(PointMasses[i]);
            }

            #endregion


            #region Pass input into curtain

            //get this frame of user input
            InputData.Update();
            //place cursor rec and draw game cursor
            MouseState currentMouseState = Mouse.GetState();
            cursorRec.X = (int)currentMouseState.X - (int)(cursorRec.Width / 2);
            cursorRec.Y = (int)currentMouseState.Y - (int)(cursorRec.Height / 2);


            //push cloth around upon collision with mouse
            for (i = 0; i < PointMasses.Count; i++)
            {   //do not allow input to be passed to pinned point masses
                if (cursorRec.Contains(PointMasses[i].X, PointMasses[i].Y))
                {   
                    if (PointMasses[i].X > currentMouseState.X)
                    { PointMasses[i].accX += 20.0f; }
                    else { PointMasses[i].accX -= 20.0f; }
                    PointMasses[i].accY -= 20.0f;

                    for (int g = 0; g < 4; g++)
                    {
                        if (PointMasses[i].neighbors[g] != null)
                        {
                            if (PointMasses[i].neighbors[g].X > currentMouseState.X)
                            { PointMasses[i].neighbors[g].accX += 20.0f; }
                            else { PointMasses[i].neighbors[g].accX -= 20.0f; }
                            PointMasses[i].neighbors[g].accY -= 20.0f;
                        }
                    }
                }
            }


            /*
            if (InputData.IsLeftMouseBtnPress())
            {   //grab one PM colliding with cursor rec
                for (i = 0; i < PointMasses.Count; i++)
                {   //do not allow input to be passed to pinned point masses
                    if (PointMasses[i].pinned == false)
                    {
                        if (cursorRec.Contains(PointMasses[i].X, PointMasses[i].Y))
                        {   //grab PM, bail
                            grabbedPM = PointMasses[i];
                            grabbedPM.pinned = true;
                            i = PointMasses.Count;
                        }
                    }
                }
            }
            if (currentMouseState.LeftButton == ButtonState.Pressed)
            {   //handle dragging state (left mouse button down)
                if (grabbedPM != null)
                {   //drag the point until released
                    grabbedPM.X = currentMouseState.X;
                    grabbedPM.Y = currentMouseState.Y;
                }
            }
            else
            {   //release grabbed pm
                if (grabbedPM != null)
                {
                    grabbedPM.pinned = false;
                    grabbedPM = null;
                }
            } 
            */

            #endregion


            //update how the lines connect to their pointMasses
            for (i = 0; i < Lines.Count; i++)
            { Functions_Simulate.UpdateLine(Lines[i]); }

            Assets.updateTimer.Stop();
            updateTimeRec.Width = (int)(Assets.updateTimer.ElapsedTicks / 1000 * 8);
            Debug.WriteLine("update ms: " + Assets.updateTimer.ElapsedMilliseconds);
        }

        protected override void Draw(GameTime gameTime)
        {
            Assets.drawTimer.Restart();
            GraphicsDevice.Clear(Color.CornflowerBlue);
            Assets.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            //draw curtain lines + cursor
            for (i = 0; i < Lines.Count; i++) { Functions_Draw.Draw(Lines[i]); }
            //Assets.spriteBatch.Draw(Assets.lineTexture, cursorRec, Color.Blue); //draw cursor's hitbox
            //draw update/draw timers

            //maxTimeRec
            Assets.spriteBatch.Draw(Assets.lineTexture, maxTimeRec, Color.Blue*0.25f);
            Assets.spriteBatch.Draw(Assets.lineTexture, updateTimeRec, Color.Red);
            Assets.spriteBatch.Draw(Assets.lineTexture, drawTimeRec, Color.Green);
            Assets.spriteBatch.End();

            Assets.drawTimer.Stop();
            drawTimeRec.Width = (int)(Assets.drawTimer.ElapsedTicks / 1000 * 8);
            base.Draw(gameTime);
        }
    }

    #endregion

}