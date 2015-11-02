﻿Public Class MainForm
    Public ReadOnly Property Version As String
        Get
            Return "Version 1.0.1_00 Beta"
        End Get
    End Property

    Public Player As Player
    Public Mouse As PointF
    Public GroundBrush As TextureBrush
    Public WallBrush As TextureBrush
    Public Random As New Random(0)
    Public ViewOffsetX As Double
    Public ViewOffsetY As Double
    Public World As World
    Public Options As Options

    Public ReadOnly Property MaxTick As Double
        Get
            Return 1 / Options.Preferences("MaxFPS")
        End Get
    End Property
    Public ReadOnly Property ScreenWidth As Integer
        Get
            Return ClientSize.Width
        End Get
    End Property
    Public ReadOnly Property ScreenHeight As Integer
        Get
            Return ClientSize.Height
        End Get
    End Property

    Public ReadOnly Property PlayerRoom As Room
        Get
            Return World.RoomAt(Player.Middle.X + Player.Room.XOffset, Player.Middle.Y + Player.Room.YOffset)
        End Get
    End Property

    Public Loaded As Boolean = False

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.DoubleBuffered = True

        'Environment.Add(New RectangleF(-1, 0, 1, ScreenHeight))
        'Environment.Add(New RectangleF(0, -1, ScreenWidth, 1))
        'Environment.Add(New RectangleF(ScreenWidth, -1, 1, ScreenHeight))
        'Environment.Add(New RectangleF(-1, ScreenHeight, ScreenWidth, -1))

        GroundBrush = New TextureBrush(My.Resources.FloorTile)
        WallBrush = New TextureBrush(My.Resources.WallStrip)

        ' Load the rooms that we have.
        Dim rooms As New List(Of Room)
        For Each s As String In IO.Directory.EnumerateFiles("Rooms\")
            If IO.Path.GetFileNameWithoutExtension(s) = "up" OrElse
                    IO.Path.GetFileNameWithoutExtension(s) = "down" OrElse
                    IO.Path.GetFileNameWithoutExtension(s) = "left" OrElse
                    IO.Path.GetFileNameWithoutExtension(s) = "right" Then
                Continue For
            End If
            Dim r As New Room(s)
            rooms.Add(r)
        Next

        LoadFiles()

        ' Generate the world to play in
        World = New World("DavidAndBen", rooms)

        ' Load the player and testing stuff
        Player = New Player(World.RoomAt(150, 150))
        'Dim TestObject1 = New NormalEnemy(PlayerRoom, 200, 200)
        'Dim TestObject2 = New NormalEnemy(PlayerRoom, 100, 100)

        World.Rooms(0).AddGameObject(Player)
        'World.Rooms(0).AddGameObject(TestObject2)
        'World.Rooms(0).AddGameObject(TestObject1)


        Loaded = True ' Keep the timer from firing until the game is done loading.
        Watch = New Stopwatch()
        Watch.Start()
    End Sub

    Private Sub LoadFiles()
        Options = New Options()
    End Sub

    Private Sub MainForm_Paint(sender As Object, e As PaintEventArgs) Handles Me.Paint
        For Each r As Room In World.Rooms
            e.Graphics.FillRectangle(WallBrush, CInt(-ViewOffsetX + r.XOffset), CInt(-ViewOffsetY + r.YOffset - 32), r.Bounds.Width, 32)
            e.Graphics.FillRectangle(GroundBrush, CInt(-ViewOffsetX + r.XOffset), CInt(-ViewOffsetY + r.YOffset), r.Bounds.Width, r.Bounds.Height)
            e.Graphics.DrawImage(My.Resources.GradientLeft, CInt(-ViewOffsetX + r.XOffset), CInt(-ViewOffsetY + r.YOffset - 32), 64, 32)
            e.Graphics.DrawImage(My.Resources.GradientRight, CInt(-ViewOffsetX + r.XOffset + r.Width - 63), CInt(-ViewOffsetY + r.YOffset - 32), 64, 32)
        Next
        For Each r As Room In World.Rooms
            For Each o As GameObject In r.GameObjects
                If o.CastsShadow Then e.Graphics.DrawImage(My.Resources.Shadow, CInt(o.Position.X - ViewOffsetX + r.XOffset), CInt(o.Position.Y + o.Image.Height - 7 - ViewOffsetY + r.YOffset), o.Image.Width, 10)
            Next
            For Each O As GameObject In r.GameObjects
                e.Graphics.DrawImage(O.Image, CInt(O.Position.X - ViewOffsetX + r.XOffset), CInt(O.Position.Y + O.Position.Z * (10 / 16) - ViewOffsetY + r.YOffset), O.Image.Width, O.Image.Height)
            Next
            e.Graphics.DrawString(IO.Path.GetFileName(r.Filename), SystemFonts.CaptionFont, Brushes.Red, CSng(-ViewOffsetX + r.XOffset), CSng(-ViewOffsetY + r.YOffset))
        Next
        e.Graphics.DrawString(Version, SystemFonts.CaptionFont, Brushes.Red, 0, 0)
        e.Graphics.DrawString(CInt(1 / Tick), SystemFonts.CaptionFont, Brushes.Red, 0, 10)
        e.Graphics.DrawString(Player.Properties("Health"), SystemFonts.CaptionFont, Brushes.Red, 0, 20)
        e.Graphics.DrawString(Player.Properties("CurrentStamina"), SystemFonts.CaptionFont, Brushes.Red, 0, 30)
    End Sub

    Private Watch As Stopwatch
    Private _tick As Double = 0.5
    Public ReadOnly Property Tick As Double
        Get
            Return _tick
        End Get
    End Property
    Private Sub Timer_Tick(sender As Object, e As EventArgs) Handles Timer.Tick
        If Loaded = False Then Exit Sub
        Invalidate()
        While Watch.Elapsed.TotalSeconds < MaxTick
            Threading.Thread.Sleep(1)
        End While
        UpdateWorld(Watch.Elapsed.TotalSeconds)
        _tick = Watch.Elapsed.TotalSeconds
        Watch.Restart()
    End Sub

    Public Sub UpdateWorld(t As Double)
        Player.UpdateMovement(t, Options.OIStatus)

        'Moving all objects
        For Each r As Room In World.Rooms
            For Each O As GameObject In r.GameObjects
                If (Not (O.Speed.X = 0 AndAlso O.Speed.Y = 0)) Then
                    Dim newx As Double = O.Position.X + (O.Speed.X * t * If(O.HitBox.Width > O.HitBox.Height, O.HitBox.Width, O.HitBox.Height))
                    Dim newy As Double = O.Position.Y + (O.Speed.Y * t * If(O.HitBox.Width > O.HitBox.Height, O.HitBox.Width, O.HitBox.Height))
                    Dim good As Boolean = True
                    For Each other As GameObject In r.GameObjects
                        If other.Equals(O) Then Continue For
                        If other.CollidesWith(O, New Vector2(newx, newy)) Then
                            good = False
                            If (O.Flags.Contains("Actor")) Then CType(O, Actor).Hit(other)
                            Exit For
                        End If
                    Next
                    If New RectangleF(0, 0, r.Width, r.Height).Contains(New RectangleF(newx + O.HitBox.X, newy + O.HitBox.Y, O.HitBox.Width, O.HitBox.Height)) = False Then good = False
                    If good Then
                        O.Position.X = newx
                        O.Position.Y = newy
                    End If
                End If
                O.Update(t)
            Next

            'Delete all items flagged for deletion
            For value As Integer = r.GameObjects.Count - 1 To 0 Step -1
                If r.GameObjects(value).Flags.Contains("Delete") Then
                    r.GameObjects.RemoveAt(value)
                End If
            Next

            For Each Objective As Objective In r.Objectives
                Objective.Update(t)
            Next

            r.ResortGameObjects()
        Next

        If IsNothing(PlayerRoom) = False Then
            If Player.Room.Equals(PlayerRoom) = False Then
                Dim oldroom As Room = Player.Room
                Dim newroom As Room = PlayerRoom
                Player.Position.X += (oldroom.XOffset - newroom.XOffset)
                Player.Position.Y += (oldroom.YOffset - newroom.YOffset)
                oldroom.GameObjects.Remove(Player)
                newroom.GameObjects.Add(Player)
                Player.Room = newroom
            End If
        End If

        ViewOffsetX = Player.Position.X + Player.Room.XOffset - (ScreenWidth / 2 - Player.HitBox.Width / 2)
        ViewOffsetY = Player.Position.Y + Player.Room.YOffset - (ScreenHeight / 2 - Player.HitBox.Height / 2)
        GroundBrush.ResetTransform()
        GroundBrush.TranslateTransform(CInt(-Player.Position.X Mod My.Resources.FloorTile.Width), CInt(-Player.Position.Y Mod My.Resources.FloorTile.Height))
        WallBrush.ResetTransform()
        WallBrush.TranslateTransform(CInt(-Player.Position.X Mod My.Resources.WallStrip.Width - 4), CInt(-Player.Position.Y Mod My.Resources.WallStrip.Height + 2))
    End Sub

    Private Sub MainForm_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        For Each key As Keys In Options.OIMap.Keys
            If (e.KeyCode = key) Then
                Options.OIStatus(Options.OIMap(key)) = True
            End If
        Next


        Select Case e.KeyCode
            Case Keys.Space
                Dim X As Integer
                Dim Y As Integer
                Select Case Player.Direction
                    Case Actor.ActorDirection.South
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X
                            Y = Player.Position.Y + Player.Image.Height + 1
                        Else
                            X = Player.Position.X
                            Y = Player.Position.Y - My.Resources.Crate.Height + 20
                        End If
                    Case Actor.ActorDirection.North
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X
                            Y = Player.Position.Y - My.Resources.Crate.Height + 16
                        Else
                            X = Player.Position.X
                            Y = Player.Position.Y + Player.Image.Height - 3
                        End If
                    Case Actor.ActorDirection.West
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y + Player.Image.Height - My.Resources.Crate.Height
                        Else
                            X = Player.Position.X + Player.Image.Width
                            Y = Player.Position.Y + Player.Image.Height - My.Resources.Crate.Height
                        End If
                    Case Actor.ActorDirection.East
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X + Player.Image.Width + 1
                            Y = Player.Position.Y + Player.Image.Height - My.Resources.Crate.Height
                        Else
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y + Player.Image.Height - My.Resources.Crate.Height
                        End If
                    Case Actor.ActorDirection.SouthWest
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y + Player.Image.Height + 1
                        Else
                            X = Player.Position.X + Player.Image.Width
                            Y = Player.Position.Y - My.Resources.Crate.Height + 20
                        End If
                    Case Actor.ActorDirection.SouthEast
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X + Player.Image.Width + 1
                            Y = Player.Position.Y + Player.Image.Height + 1
                        Else
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y - My.Resources.Crate.Height + 20
                        End If
                    Case Actor.ActorDirection.NorthWest
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y - My.Resources.Crate.Height + 16
                        Else
                            X = Player.Position.X + Player.Image.Width
                            Y = Player.Position.Y + Player.Image.Height - 3
                        End If
                    Case Actor.ActorDirection.NorthEast
                        If (Not Options.OIStatus("Up") Or Options.Preferences("PlayerMovementType") = "ArcadeMovement") Then
                            X = Player.Position.X + Player.Image.Width + 1
                            Y = Player.Position.Y - My.Resources.Crate.Height + 16
                        Else
                            X = Player.Position.X - My.Resources.Crate.Width - 1
                            Y = Player.Position.Y + Player.Image.Height - 3
                        End If
                End Select
                Dim newcrate As New GameObject(My.Resources.Crate, Player.Room, New Vector3(X, Y, 0), 10)
                Dim good As Boolean = True

                For Each o As GameObject In Player.Room.GameObjects
                    If o.CollidesWith(newcrate, New Vector2(X, Y)) Then
                        good = False
                        Exit For
                    End If
                Next
                If good Then
                    Player.Room.AddGameObject(newcrate)
                End If
        End Select
    End Sub

    Private Sub MainForm_KeyUp(sender As Object, e As KeyEventArgs) Handles Me.KeyUp
        For Each key As Keys In Options.OIMap.Keys
            If (e.KeyCode = key) Then
                Options.OIStatus(Options.OIMap(key)) = False
            End If
        Next
    End Sub

    Protected Overrides Function IsInputKey(
        ByVal keyData As Keys) As Boolean
        Return True

    End Function

    Private Sub MainForm_MouseClick(sender As Object, e As MouseEventArgs) Handles Me.MouseClick
        Select Case e.Button
            Case MouseButtons.Left
                For Each gameObject As GameObject In Player.getNearList(Player.Properties("AttackRange"), Player.Properties("AttackAngle"))
                    Player.Hit(gameObject)
                Next

        End Select
    End Sub

    Private Sub MainForm_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        Dim direction As Double = Player.getDirectionTo(e.Location + New Size(ViewOffsetX, ViewOffsetY) - New Size(Player.Room.XOffset, Player.Room.YOffset))
        Player.Direction = Actor.ToActorDirection(direction)
        Mouse = e.Location + New Size(ViewOffsetX, ViewOffsetY) - New Size(Player.Room.XOffset, Player.Room.YOffset)
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Options.SaveOptions()
    End Sub
End Class
