using System;
using System.Collections.Specialized;
using System.Windows.Forms;
using CodeWalker.Properties;
using SharpDX;
using SharpDX.XInput;

namespace CodeWalker;

public class InputManager
{
    public bool CtrlPressed;
    public volatile bool kbjump;
    public volatile bool kbmovebck;
    public volatile bool kbmovedn;


    public volatile bool kbmovefwd;
    public volatile bool kbmovelft;
    public volatile bool kbmovergt;
    public volatile bool kbmoveup;
    public volatile bool kbmoving;

    public KeyBindings keyBindings = new(Settings.Default.KeyBindings);
    public bool ShiftPressed;
    public Controller xbcontroller;
    public State xbcontrollerstate;
    public State xbcontrollerstateprev;
    public float xbcontrolvelocity = 0.0f;

    public bool xbenable;
    public float xblt; //left trigger value
    public float xblx; //left stick X axis
    public float xbly; //left stick Y axis
    public Vector4 xbmainaxes = Vector4.Zero;
    public Vector4 xbmainaxesprev = Vector4.Zero;
    public float xbrt; //right trigger value
    public float xbrx; //right stick X axis
    public float xbry; //right stick Y axis
    public Vector2 xbtrigs = Vector2.Zero;
    public Vector2 xbtrigsprev = Vector2.Zero;


    public void Init()
    {
        xbcontroller = new Controller(UserIndex.One);
        if (!xbcontroller.IsConnected)
        {
            var controllers = new[]
                { new Controller(UserIndex.Two), new Controller(UserIndex.Three), new Controller(UserIndex.Four) };
            foreach (var selectControler in controllers)
                if (selectControler.IsConnected)
                {
                    xbcontroller = selectControler;
                    xbcontrollerstate = xbcontroller.GetState();
                    xbcontrollerstateprev = xbcontrollerstate;
                    break;
                }
        }
        else
        {
            xbcontrollerstate = xbcontroller.GetState();
            xbcontrollerstateprev = xbcontrollerstate;
        }
    }


    public void Update()
    {
        var s = Settings.Default;

        xbenable = xbcontroller != null && xbcontroller.IsConnected;
        xblx = 0;
        xbly = 0;
        xbrx = 0;
        xbry = 0;
        xblt = 0;
        xbrt = 0; //input axes

        if (xbenable)
        {
            xbcontrollerstateprev = xbcontrollerstate;
            xbcontrollerstate = xbcontroller.GetState();
            xbmainaxesprev = xbmainaxes;
            xbtrigsprev = xbtrigs;
            xbmainaxes = ControllerMainAxes();
            xbtrigs = ControllerTriggers();
            xblx = xbmainaxes.X;
            xbly = xbmainaxes.Y;
            xbrx = xbmainaxes.Z;
            xbry = xbmainaxes.W;
            xblt = xbtrigs.X;
            xbrt = xbtrigs.Y;
            var lamt = s.XInputLThumbSensitivity;
            var ramt = s.XInputRThumbSensitivity;
            xbly = s.XInputLThumbInvert ? xbly : -xbly;
            xbry = s.XInputRThumbInvert ? xbry : -xbry;
            xblx *= lamt;
            xbly *= lamt;
            xbrx *= ramt;
            xbry *= ramt;
        }
    }


    public void KeyDown(KeyEventArgs e, bool enablemove)
    {
        var k = e.KeyCode;
        CtrlPressed = (e.Modifiers & Keys.Control) > 0;
        ShiftPressed = (e.Modifiers & Keys.Shift) > 0;

        //enablemove = enablemove && (!ctrl);

        //WASD move...
        if (enablemove)
        {
            if (k == keyBindings.MoveForward) kbmovefwd = true;
            if (k == keyBindings.MoveBackward) kbmovebck = true;
            if (k == keyBindings.MoveLeft) kbmovelft = true;
            if (k == keyBindings.MoveRight) kbmovergt = true;
            if (k == keyBindings.MoveUp) kbmoveup = true;
            if (k == keyBindings.MoveDown) kbmovedn = true;
            if (k == keyBindings.Jump) kbjump = true;
        }

        kbmoving = kbmovefwd || kbmovebck || kbmovelft || kbmovergt || kbmoveup || kbmovedn || kbjump;
    }

    public void KeyUp(KeyEventArgs e)
    {
        CtrlPressed = (e.Modifiers & Keys.Control) > 0;
        ShiftPressed = (e.Modifiers & Keys.Shift) > 0;

        var k = e.KeyCode;
        if (k == keyBindings.MoveForward) kbmovefwd = false;
        if (k == keyBindings.MoveBackward) kbmovebck = false;
        if (k == keyBindings.MoveLeft) kbmovelft = false;
        if (k == keyBindings.MoveRight) kbmovergt = false;
        if (k == keyBindings.MoveUp) kbmoveup = false;
        if (k == keyBindings.MoveDown) kbmovedn = false;
        if (k == keyBindings.Jump) kbjump = false;

        kbmoving = kbmovefwd || kbmovebck || kbmovelft || kbmovergt || kbmoveup || kbmovedn || kbjump;
    }

    public void KeyboardStop()
    {
        kbmovefwd = false;
        kbmovebck = false;
        kbmovelft = false;
        kbmovergt = false;
        kbmoveup = false;
        kbmovedn = false;
        kbjump = false;
    }


    public Vector3 KeyboardMoveVec(bool mapview = false)
    {
        var movevec = Vector3.Zero;
        if (mapview)
        {
            if (kbmovefwd) movevec.Y += 1.0f;
            if (kbmovebck) movevec.Y -= 1.0f;
            if (kbmovelft) movevec.X -= 1.0f;
            if (kbmovergt) movevec.X += 1.0f;
            if (kbmoveup) movevec.Y += 1.0f;
            if (kbmovedn) movevec.Y -= 1.0f;
        }
        else
        {
            if (kbmovefwd) movevec.Z -= 1.0f;
            if (kbmovebck) movevec.Z += 1.0f;
            if (kbmovelft) movevec.X -= 1.0f;
            if (kbmovergt) movevec.X += 1.0f;
            if (kbmoveup) movevec.Y += 1.0f;
            if (kbmovedn) movevec.Y -= 1.0f;
        }

        return movevec;
    }


    public Vector4 ControllerMainAxes()
    {
        var gp = xbcontrollerstate.Gamepad;
        var ldz = Gamepad.LeftThumbDeadZone;
        var rdz = Gamepad.RightThumbDeadZone;
        float ltnrng = -(short.MinValue + ldz);
        float ltprng = short.MaxValue - ldz;
        float rtnrng = -(short.MinValue + rdz);
        float rtprng = short.MaxValue - rdz;

        var lx = gp.LeftThumbX < 0 ? Math.Min((gp.LeftThumbX + ldz) / ltnrng, 0) :
            gp.LeftThumbX > 0 ? Math.Max((gp.LeftThumbX - ldz) / ltprng, 0) : 0;
        var ly = gp.LeftThumbY < 0 ? Math.Min((gp.LeftThumbY + ldz) / ltnrng, 0) :
            gp.LeftThumbY > 0 ? Math.Max((gp.LeftThumbY - ldz) / ltprng, 0) : 0;
        var rx = gp.RightThumbX < 0 ? Math.Min((gp.RightThumbX + rdz) / rtnrng, 0) :
            gp.RightThumbX > 0 ? Math.Max((gp.RightThumbX - rdz) / rtprng, 0) : 0;
        var ry = gp.RightThumbY < 0 ? Math.Min((gp.RightThumbY + rdz) / rtnrng, 0) :
            gp.RightThumbY > 0 ? Math.Max((gp.RightThumbY - rdz) / rtprng, 0) : 0;

        return new Vector4(lx, ly, rx, ry);
    }

    public Vector2 ControllerTriggers()
    {
        var gp = xbcontrollerstate.Gamepad;
        var tt = Gamepad.TriggerThreshold;
        float trng = byte.MaxValue - tt;
        var lt = Math.Max((gp.LeftTrigger - tt) / trng, 0);
        var rt = Math.Max((gp.RightTrigger - tt) / trng, 0);
        return new Vector2(lt, rt);
    }

    public bool ControllerButtonPressed(GamepadButtonFlags b)
    {
        return (xbcontrollerstate.Gamepad.Buttons & b) != 0;
    }

    public bool ControllerButtonJustPressed(GamepadButtonFlags b)
    {
        return (xbcontrollerstate.Gamepad.Buttons & b) != 0 && (xbcontrollerstateprev.Gamepad.Buttons & b) == 0;
    }
}

public class KeyBindings
{
    public Keys EditPosition = Keys.W;
    public Keys EditRotation = Keys.E;
    public Keys EditScale = Keys.R;
    public Keys ExitEditMode = Keys.Q;
    public Keys FirstPerson = Keys.P;
    public Keys Jump = Keys.Space; //for control mode
    public Keys MoveBackward = Keys.S;
    public Keys MoveDown = Keys.F;
    public Keys MoveFasterZoomOut = Keys.X;
    public Keys MoveForward = Keys.W;
    public Keys MoveLeft = Keys.A;
    public Keys MoveRight = Keys.D;
    public Keys MoveSlowerZoomIn = Keys.Z;
    public Keys MoveUp = Keys.R;
    public Keys ToggleMouseSelect = Keys.C;
    public Keys ToggleToolbar = Keys.T;

    public KeyBindings(StringCollection sc)
    {
        foreach (var s in sc)
        {
            var parts = s.Split(':');
            if (parts.Length == 2)
            {
                var sval = parts[1].Trim();
                var k = (Keys)Enum.Parse(typeof(Keys), sval);
                SetBinding(parts[0], k);
            }
        }
    }

    public void SetBinding(string name, Keys k)
    {
        switch (name)
        {
            case "Move Forwards":
                MoveForward = k;
                break;
            case "Move Backwards":
                MoveBackward = k;
                break;
            case "Move Left":
                MoveLeft = k;
                break;
            case "Move Right":
                MoveRight = k;
                break;
            case "Move Up":
                MoveUp = k;
                break;
            case "Move Down":
                MoveDown = k;
                break;
            case "Move Slower / Zoom In":
                MoveSlowerZoomIn = k;
                break;
            case "Move Faster / Zoom Out":
                MoveFasterZoomOut = k;
                break;
            case "Toggle Mouse Select":
                ToggleMouseSelect = k;
                break;
            case "Toggle Toolbar":
                ToggleToolbar = k;
                break;
            case "Exit Edit Mode":
                ExitEditMode = k;
                break;
            case "Edit Position":
                EditPosition = k;
                break;
            case "Edit Rotation":
                EditRotation = k;
                break;
            case "Edit Scale":
                EditScale = k;
                break;
            case "First Person Mode":
                FirstPerson = k;
                break;
        }
    }

    public StringCollection GetSetting()
    {
        var sc = new StringCollection();
        sc.Add(GetSettingItem("Move Forwards", MoveForward));
        sc.Add(GetSettingItem("Move Backwards", MoveBackward));
        sc.Add(GetSettingItem("Move Left", MoveLeft));
        sc.Add(GetSettingItem("Move Right", MoveRight));
        sc.Add(GetSettingItem("Move Up", MoveUp));
        sc.Add(GetSettingItem("Move Down", MoveDown));
        sc.Add(GetSettingItem("Move Slower / Zoom In", MoveSlowerZoomIn));
        sc.Add(GetSettingItem("Move Faster / Zoom Out", MoveFasterZoomOut));
        sc.Add(GetSettingItem("Toggle Mouse Select", ToggleMouseSelect));
        sc.Add(GetSettingItem("Toggle Toolbar", ToggleToolbar));
        sc.Add(GetSettingItem("Exit Edit Mode", ExitEditMode));
        sc.Add(GetSettingItem("Edit Position", EditPosition));
        sc.Add(GetSettingItem("Edit Rotation", EditRotation));
        sc.Add(GetSettingItem("Edit Scale", EditScale));
        sc.Add(GetSettingItem("First Person Mode", FirstPerson));
        return sc;
    }

    private string GetSettingItem(string name, Keys val)
    {
        return name + ": " + val;
    }

    public KeyBindings Copy()
    {
        return (KeyBindings)MemberwiseClone();
    }
}