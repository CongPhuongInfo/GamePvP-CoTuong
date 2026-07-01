Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9988

    ' === Kich thuoc ban co ===
    ' Ban co Tuong: 9 cot x 10 hang (duong ke 8x9, o tinh tu tam duong ke)
    Private Const SQ As Integer = 60          ' khoang cach giua 2 duong ke (px)
    Private Const MARGIN As Integer = 48      ' le xung quanh
    ' Kich thuoc vung ve ban co
    Private Const BOARD_W As Integer = MARGIN * 2 + SQ * 8   ' 8 khoang = 9 duong doc
    Private Const BOARD_H As Integer = MARGIN * 2 + SQ * 9   ' 9 khoang = 10 duong ngang
    Private Const FORM_W As Integer = BOARD_W + 200
    Private Const FORM_H As Integer = 980     ' cao them de cho card nguoi choi + khung chat

    Private game As XiangqiGame
    Private peer As NetworkPeer
    Private isHost As Boolean
    Private localPlayer As Integer = -1

    Private selectedRow As Integer = -1
    Private selectedCol As Integer = -1
    Private currentLegalMoves As New List(Of XiangqiGame.XqMove)()

    ' === Toa do trung tam moi o tren man hinh ===
    ' cellCenter(row, col) -> Point (pixel) la tam giao diem luoi
    Private cellCenter(9, 8) As Point

    ' === UI ket noi ===
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As Panel
    Private lblTurn As Label
    Private lblYouAre As Label
    Private btnRestart As Button

    ' === Card thong tin nguoi choi (Do / Den) ===
    Private Shared ReadOnly SideColor() As Color = {Color.FromArgb(190, 40, 40), Color.FromArgb(40, 40, 40)}
    Private Shared ReadOnly SideNameVN() As String = {"DO", "DEN"}
    Private pnlPlayers(1) As Panel
    Private lblCardStatus(1) As Label
    Private lblCardTag(1) As Label

    ' === Khung chat (gop chung voi log he thong) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button

    Public Sub New()
        BuildCellCenters()
        InitUI()
    End Sub

    ' ============================================================
    '  TINH TAM O
    ' ============================================================
    Private Sub BuildCellCenters()
        Dim row As Integer, col As Integer
        For row = 0 To 9
            For col = 0 To 8
                ' Neu la Do (host, hang duoi), lat doc de hang Do o phia duoi man hinh
                Dim screenRow As Integer = If(localPlayer = XiangqiGame.RED, 9 - row, row)
                Dim screenCol As Integer = col
                cellCenter(row, col) = New Point(MARGIN + screenCol * SQ, MARGIN + screenRow * SQ)
            Next col
        Next row
    End Sub

    ' ============================================================
    '  INIT UI
    ' ============================================================
    Private Sub InitUI()
        Me.Text = "Co Tuong Online"
        Me.ClientSize = New Size(FORM_W, FORM_H)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(240, 225, 190)

        BuildConnectPanel()
        BuildGamePanel()
        pnlGame.Visible = False
    End Sub

    ' ============================================================
    '  CONNECT PANEL
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(240, 225, 190)

        Dim lbl As New Label()
        lbl.Text = "CO TUONG ONLINE" : lbl.Font = New Font("Segoe UI", 20.0!, FontStyle.Bold)
        lbl.ForeColor = Color.FromArgb(160, 30, 30)
        lbl.Location = New Point(120, 70) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim lPort As New Label() : lPort.Text = "Port:" : lPort.Location = New Point(200, 160) : lPort.AutoSize = True
        pnlConnect.Controls.Add(lPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Location = New Point(260, 157) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host - choi DO)"
        btnHost.Location = New Point(170, 200) : btnHost.Size = New Size(270, 40)
        btnHost.BackColor = Color.FromArgb(200, 60, 60) : btnHost.ForeColor = Color.White
        btnHost.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lIP As New Label() : lIP.Text = "IP Host:" : lIP.Location = New Point(200, 265) : lIP.AutoSize = True
        pnlConnect.Controls.Add(lIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1"
        txtIP.Location = New Point(270, 262) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Khach - choi DEN)"
        btnJoin.Location = New Point(170, 305) : btnJoin.Size = New Size(270, 40)
        btnJoin.BackColor = Color.FromArgb(30, 60, 120) : btnJoin.ForeColor = Color.White
        btnJoin.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label() : lblStatus.Location = New Point(130, 380) : lblStatus.AutoSize = True
        lblStatus.ForeColor = Color.DimGray
        lblStatus.Text = "Host: bam 'Tao phong', ban se choi DO, di truoc." & Environment.NewLine &
                         "Khach: nhap IP roi bam 'Vao phong', ban se choi DEN."
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    ' ============================================================
    '  GAME PANEL
    ' ============================================================
    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Location = New Point(0, 0)
        pnlGame.Size = New Size(FORM_W, FORM_H)
        pnlGame.BackColor = Color.FromArgb(240, 225, 190)

        boardPanel = New Panel()
        boardPanel.Location = New Point(4, 4)
        boardPanel.Size = New Size(BOARD_W + 4, BOARD_H + 4)
        boardPanel.BackColor = Color.FromArgb(220, 195, 145)
        boardPanel.Cursor = Cursors.Hand
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        AddHandler boardPanel.MouseDown, AddressOf BoardPanel_MouseDown
        pnlGame.Controls.Add(boardPanel)

        Dim rightX As Integer = BOARD_W + 14

        lblYouAre = New Label()
        lblYouAre.Location = New Point(rightX, 10) : lblYouAre.AutoSize = True
        lblYouAre.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        pnlGame.Controls.Add(lblYouAre)

        lblTurn = New Label()
        lblTurn.Location = New Point(rightX, 40) : lblTurn.Width = 180
        lblTurn.Height = 60 : lblTurn.Font = New Font("Segoe UI", 9.5!)
        pnlGame.Controls.Add(lblTurn)

        btnRestart = New Button() : btnRestart.Text = "Choi lai (Host)"
        btnRestart.Location = New Point(rightX, 115) : btnRestart.Size = New Size(160, 36)
        btnRestart.BackColor = Color.FromArgb(160, 30, 30) : btnRestart.ForeColor = Color.White
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        ' === Card "Do / Den" (giong kieu Co Vua) ===
        Dim cardY As Integer = 4 + BOARD_H + 4 + 10
        Dim cardW As Integer = (FORM_W - 20 - 10) \ 2
        pnlPlayers(XiangqiGame.RED) = BuildPlayerCard(XiangqiGame.RED, New Point(4, cardY), cardW)
        pnlGame.Controls.Add(pnlPlayers(XiangqiGame.RED))
        pnlPlayers(XiangqiGame.BLK) = BuildPlayerCard(XiangqiGame.BLK, New Point(4 + cardW + 10, cardY), cardW)
        pnlGame.Controls.Add(pnlPlayers(XiangqiGame.BLK))

        ' === Khung chat, gop chung voi log he thong ===
        Dim chatY As Integer = cardY + 58 + 10
        Dim chatH As Integer = FORM_H - chatY - 12
        BuildChatPanel(4, FORM_W - 8, chatY, chatH)

        Me.Controls.Add(pnlGame)
    End Sub

    ''' <summary>Tao 1 "card" nho hien mau ben + trang thai (dang di / dang cho / Ban) cho 1 ben.</summary>
    Private Function BuildPlayerCard(side As Integer, loc As Point, w As Integer) As Panel
        Dim card As New Panel()
        card.Location = loc : card.Size = New Size(w, 58)
        card.BackColor = Color.White
        card.BorderStyle = BorderStyle.FixedSingle

        Dim bar As New Panel()
        bar.Location = New Point(0, 0) : bar.Size = New Size(6, 58)
        bar.BackColor = SideColor(side)
        card.Controls.Add(bar)

        lblCardTag(side) = New Label()
        lblCardTag(side).Text = SideNameVN(side)
        lblCardTag(side).Font = New Font("Segoe UI", 9.5!, FontStyle.Bold)
        lblCardTag(side).ForeColor = If(side = XiangqiGame.RED, Color.FromArgb(180, 30, 30), Color.Black)
        lblCardTag(side).Location = New Point(16, 4) : lblCardTag(side).AutoSize = True
        card.Controls.Add(lblCardTag(side))

        lblCardStatus(side) = New Label()
        lblCardStatus(side).Text = ""
        lblCardStatus(side).Font = New Font("Segoe UI", 9.0!)
        lblCardStatus(side).ForeColor = Color.DimGray
        lblCardStatus(side).Location = New Point(16, 24) : lblCardStatus(side).AutoSize = True
        lblCardStatus(side).MaximumSize = New Size(w - 24, 30)
        card.Controls.Add(lblCardStatus(side))

        Return card
    End Function

    ''' <summary>Khung chat: ListBox hien tin nhan (ca chat va log he thong) + TextBox go + nut Gui.</summary>
    Private Sub BuildChatPanel(x As Integer, w As Integer, y As Integer, h As Integer)
        pnlChat = New Panel()
        pnlChat.Location = New Point(x, y)
        pnlChat.Size = New Size(w, h)

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0)
        lstChat.Size = New Size(w, h - 30)
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, h - 26)
        txtChatInput.Size = New Size(w - 55, 24)
        AddHandler txtChatInput.KeyDown, Sub(s As Object, ev As KeyEventArgs)
            If ev.KeyCode = Keys.Enter Then
                BtnSend_Click(s, EventArgs.Empty)
                ev.Handled = True
                ev.SuppressKeyPress = True
            End If
        End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(w - 50, h - 27)
        btnSend.Size = New Size(50, 26)
        AddHandler btnSend.Click, AddressOf BtnSend_Click
        pnlChat.Controls.Add(btnSend)

        pnlGame.Controls.Add(pnlChat)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs)
        If txtChatInput.Text.Trim() = "" Then Return
        If localPlayer < 0 Then Return
        Dim tag As String = SideNameVN(localPlayer)
        Dim msg As String = txtChatInput.Text.Trim()
        AppendChat(tag & ": " & msg)

        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("CHAT:" & tag & ":" & msg)
        End If

        txtChatInput.Text = ""
        txtChatInput.Focus()
    End Sub

    Private Sub AppendChat(msg As String)
        If lstChat Is Nothing Then Return
        lstChat.Items.Add(msg)
        lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1)
    End Sub

    ' ============================================================
    '  VE BAN CO TUONG (GDI)
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias

        ' Nen ban co
        Using br As New SolidBrush(Color.FromArgb(220, 195, 145))
            g.FillRectangle(br, boardPanel.ClientRectangle)
        End Using

        DrawBoardLines(g)
        DrawPalaceLines(g)
        DrawRiverText(g)
        DrawCoordinateLabels(g)
        DrawHighlightsAndPieces(g)
    End Sub

    Private Sub DrawBoardLines(g As Graphics)
        Using pen As New Pen(Color.FromArgb(80, 50, 20), 1.5F)
            ' Duong ngang: 10 hang (row 0..9)
            Dim row As Integer
            For row = 0 To 9
                Dim y As Integer = MARGIN + row * SQ
                g.DrawLine(pen, MARGIN, y, MARGIN + 8 * SQ, y)
            Next row

            ' Duong doc: 9 cot (col 0..8)
            ' Cac cot bien (0 va 8) di xuyen qua toan bo
            g.DrawLine(pen, MARGIN, MARGIN, MARGIN, MARGIN + 9 * SQ)
            g.DrawLine(pen, MARGIN + 8 * SQ, MARGIN, MARGIN + 8 * SQ, MARGIN + 9 * SQ)

            ' Cot giua (1..7) chi ve 2 doan: hang 0..4 va hang 5..9 (cat ra o song)
            Dim col As Integer
            For col = 1 To 7
                Dim x As Integer = MARGIN + col * SQ
                ' Nua tren (hang 0-4)
                g.DrawLine(pen, x, MARGIN, x, MARGIN + 4 * SQ)
                ' Nua duoi (hang 5-9)
                g.DrawLine(pen, x, MARGIN + 5 * SQ, x, MARGIN + 9 * SQ)
            Next col
        End Using
    End Sub

    Private Sub DrawPalaceLines(g As Graphics)
        Using pen As New Pen(Color.FromArgb(80, 50, 20), 1.5F)
            ' Cung tren (hang 0-2, cot 3-5)
            Dim x3 As Integer = MARGIN + 3 * SQ
            Dim x5 As Integer = MARGIN + 5 * SQ
            Dim y0 As Integer = MARGIN
            Dim y2 As Integer = MARGIN + 2 * SQ
            g.DrawLine(pen, x3, y0, x5, y2)
            g.DrawLine(pen, x5, y0, x3, y2)

            ' Cung duoi (hang 7-9, cot 3-5)
            Dim y7 As Integer = MARGIN + 7 * SQ
            Dim y9 As Integer = MARGIN + 9 * SQ
            g.DrawLine(pen, x3, y7, x5, y9)
            g.DrawLine(pen, x5, y7, x3, y9)
        End Using
    End Sub

    Private Sub DrawRiverText(g As Graphics)
        Using fnt As New Font("Segoe UI", 12.0!, FontStyle.Bold)
        Using br As New SolidBrush(Color.FromArgb(100, 60, 20))
            Dim riverY As Integer = MARGIN + 4 * SQ + SQ \ 2 - 12
            ' Chu "楚河"
            Dim s1 As String = "Ha   Song"
            Dim s2 As String = "Gioi   Han"
            Dim sz1 As SizeF = g.MeasureString(s1, fnt)
            Dim sz2 As SizeF = g.MeasureString(s2, fnt)
            g.DrawString(s1, fnt, br, (MARGIN + MARGIN + 8 * SQ) \ 2 - sz1.Width - 10, CSng(riverY))
            g.DrawString(s2, fnt, br, (MARGIN + MARGIN + 8 * SQ) \ 2 + 10, CSng(riverY))
        End Using
        End Using
    End Sub

    Private Sub DrawCoordinateLabels(g As Graphics)
        Using fnt As New Font("Segoe UI", 8.0!)
        Using br As New SolidBrush(Color.FromArgb(90, 60, 30))
            ' Nhan cot (a-i) phia duoi
            Dim col As Integer
            For col = 0 To 8
                Dim ch As String = Chr(Asc("a"c) + col).ToString()
                Dim x As Single = CSng(MARGIN + col * SQ - 4)
                g.DrawString(ch, fnt, br, x, CSng(MARGIN + 9 * SQ + 6))
            Next col
            ' Nhan hang (1-10) ben phai
            Dim row As Integer
            For row = 0 To 9
                Dim screenRow As Integer = If(localPlayer = XiangqiGame.RED, 9 - row, row)
                Dim lbl As String = (10 - row).ToString()
                g.DrawString(lbl, fnt, br, CSng(MARGIN + 8 * SQ + 8), CSng(MARGIN + screenRow * SQ - 7))
            Next row
        End Using
        End Using
    End Sub

    Private Sub DrawHighlightsAndPieces(g As Graphics)
        Dim row As Integer, col As Integer
        ' Ve highlight nen truoc
        For row = 0 To 9
            For col = 0 To 8
                Dim pt As Point = cellCenter(row, col)
                Dim isSelected As Boolean = (row = selectedRow AndAlso col = selectedCol)
                Dim isDest As Boolean = IsHighlightedDest(row, col)
                Dim isCheck As Boolean = IsKingCheckSquare(row, col)

                If isCheck Then
                    Using br As New SolidBrush(Color.FromArgb(160, 200, 50, 50))
                        g.FillEllipse(br, pt.X - SQ \ 2 + 4, pt.Y - SQ \ 2 + 4, SQ - 8, SQ - 8)
                    End Using
                ElseIf isSelected Then
                    Using br As New SolidBrush(Color.FromArgb(160, 80, 200, 80))
                        g.FillEllipse(br, pt.X - SQ \ 2 + 4, pt.Y - SQ \ 2 + 4, SQ - 8, SQ - 8)
                    End Using
                End If

                If isDest Then
                    Dim pieceHere As Integer = If(game IsNot Nothing, game.GetPieceAt(row, col), 0)
                    If pieceHere = 0 Then
                        ' Cham tron nho
                        Dim d As Integer = 12
                        Using br As New SolidBrush(Color.FromArgb(180, 30, 30, 30))
                            g.FillEllipse(br, pt.X - d \ 2, pt.Y - d \ 2, d, d)
                        End Using
                    Else
                        ' Vong tron xung quanh quan co the an
                        Using pen As New Pen(Color.FromArgb(200, 200, 40, 40), 3)
                            g.DrawEllipse(pen, pt.X - SQ \ 2 + 4, pt.Y - SQ \ 2 + 4, SQ - 9, SQ - 9)
                        End Using
                    End If
                End If
            Next col
        Next row

        ' Ve quan co
        For row = 0 To 9
            For col = 0 To 8
                Dim pieceVal As Integer = If(game IsNot Nothing, game.GetPieceAt(row, col), 0)
                If pieceVal <> 0 Then
                    DrawPiece(g, cellCenter(row, col), pieceVal)
                End If
            Next col
        Next row
    End Sub

    ''' <summary>Ve quan co kieu truyen thong: hinh tron co chu Han ben trong.</summary>
    Private Sub DrawPiece(g As Graphics, center As Point, pieceVal As Integer)
        Dim radius As Integer = SQ \ 2 - 4
        Dim isDo As Boolean = (pieceVal > 0)

        ' Mau nen va vien quan
        Dim fillColor As Color = If(isDo, Color.FromArgb(230, 200, 150), Color.FromArgb(40, 40, 40))
        Dim borderColor As Color = If(isDo, Color.FromArgb(160, 30, 30), Color.FromArgb(10, 10, 10))
        Dim textColor As Color = If(isDo, Color.FromArgb(180, 30, 30), Color.FromArgb(20, 140, 200))

        Dim rect As New Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2)

        ' Do bong nhe
        Using shBr As New SolidBrush(Color.FromArgb(60, 0, 0, 0))
            g.FillEllipse(shBr, rect.X + 2, rect.Y + 2, rect.Width, rect.Height)
        End Using

        ' Nen quan
        Using br As New SolidBrush(fillColor)
            g.FillEllipse(br, rect)
        End Using

        ' Vien ngoai
        Using pen As New Pen(borderColor, 2.5F)
            g.DrawEllipse(pen, rect)
        End Using

        ' Vien trong (trang tiet)
        Dim inner As New Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6)
        Using pen2 As New Pen(Color.FromArgb(100, borderColor), 1.0F)
            g.DrawEllipse(pen2, inner)
        End Using

        ' Chu Han
        Dim glyph As String = XiangqiGame.GetGlyphName(pieceVal)
        If glyph <> "" Then
            Dim fontSize As Single = CSng(SQ) * 0.38F
            Using fnt As New Font("SimSun", fontSize, FontStyle.Bold)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                Using tBr As New SolidBrush(textColor)
                    g.DrawString(glyph, fnt, tBr, CSng(center.X), CSng(center.Y), sf)
                End Using
            End Using
        End If
    End Sub

    ' ============================================================
    '  CLICK TREN BAN CO
    ' ============================================================
    Private Function HitTest(location As Point) As Point
        ' Tra ve (row, col) gan nhat voi diem click (trong ban kip SQ/2)
        Dim row As Integer, col As Integer
        For row = 0 To 9
            For col = 0 To 8
                Dim ctr As Point = cellCenter(row, col)
                Dim dx As Integer = location.X - ctr.X
                Dim dy As Integer = location.Y - ctr.Y
                If dx * dx + dy * dy <= (SQ \ 2 - 2) * (SQ \ 2 - 2) Then
                    Return New Point(col, row)  ' X=col, Y=row
                End If
            Next col
        Next row
        Return New Point(-1, -1)
    End Function

    Private Sub BoardPanel_MouseDown(sender As Object, e As MouseEventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localPlayer Then
            AppendLog("Chua den luot ban.")
            Return
        End If

        Dim hit As Point = HitTest(e.Location)
        If hit.X = -1 Then Return
        Dim col As Integer = hit.X
        Dim row As Integer = hit.Y

        If selectedRow = -1 Then
            TrySelect(row, col)
        Else
            If row = selectedRow AndAlso col = selectedCol Then
                ClearSelection()
            ElseIf IsOwnPiece(row, col) Then
                TrySelect(row, col)
            ElseIf IsHighlightedDest(row, col) Then
                AttemptMoveTo(row, col)
            Else
                ClearSelection()
            End If
        End If

        boardPanel.Invalidate()
    End Sub

    Private Function IsOwnPiece(row As Integer, col As Integer) As Boolean
        If game Is Nothing Then Return False
        Dim p As Integer = game.GetPieceAt(row, col)
        If p = 0 Then Return False
        Return game.PieceColorAt(row, col) = localPlayer
    End Function

    Private Sub TrySelect(row As Integer, col As Integer)
        If Not IsOwnPiece(row, col) Then
            ClearSelection() : Return
        End If
        selectedRow = row : selectedCol = col
        currentLegalMoves = game.GetLegalMoves(row, col)
    End Sub

    Private Sub ClearSelection()
        selectedRow = -1 : selectedCol = -1
        currentLegalMoves = New List(Of XiangqiGame.XqMove)()
    End Sub

    Private Sub AttemptMoveTo(toRow As Integer, toCol As Integer)
        Dim fromRow As Integer = selectedRow
        Dim fromCol As Integer = selectedCol
        ClearSelection()

        If isHost Then
            Dim errMsg As String = ""
            If game.TryMove(localPlayer, fromRow, fromCol, toRow, toCol, errMsg) Then
                AppendLog(game.LastLog)
                BroadcastState()
                CheckAndShowGameOver()
            Else
                AppendLog("Loi: " & errMsg)
            End If
            RefreshUI()
        Else
            Dim sb As New System.Text.StringBuilder()
            sb.Append("MOVEREQ:") : sb.Append(localPlayer.ToString()) : sb.Append(":")
            sb.Append(fromRow.ToString()) : sb.Append(":") : sb.Append(fromCol.ToString()) : sb.Append(":")
            sb.Append(toRow.ToString()) : sb.Append(":") : sb.Append(toCol.ToString())
            peer.SendLine(sb.ToString())
        End If
    End Sub

    Private Function IsHighlightedDest(row As Integer, col As Integer) As Boolean
        Dim mv As XiangqiGame.XqMove
        For Each mv In currentLegalMoves
            If mv.ToRow = row AndAlso mv.ToCol = col Then Return True
        Next mv
        Return False
    End Function

    Private Function IsKingCheckSquare(row As Integer, col As Integer) As Boolean
        If game Is Nothing OrElse game.GameOver Then Return False
        Dim cp As Integer = game.CurrentPlayer
        If Not game.IsKingInCheck(cp) Then Return False
        Dim pieceVal As Integer = game.GetPieceAt(row, col)
        If pieceVal = 0 Then Return False
        If Math.Abs(pieceVal) <> XiangqiGame.PT_TUONG Then Return False
        Return game.PieceColorAt(row, col) = cp
    End Function

    ' ============================================================
    '  KET NOI MANG
    ' ============================================================
    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        isHost = True : localPlayer = -1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        Try
            peer.StartHost(port)
            lblStatus.Text = "Dang cho doi thu tren port " & port.ToString() & "..."
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return
        isHost = False : localPlayer = XiangqiGame.BLK
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If Not isHost Then peer.SendLine("HELLO:Client")
    End Sub

    Private Sub Peer_Disconnected()
        MessageBox.Show("Mat ket noi.")
        pnlGame.Visible = False : pnlConnect.Visible = True
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                game = New XiangqiGame() : localPlayer = XiangqiGame.RED
                ShowGamePanel() : BroadcastState()
                AppendLog("Doi thu vao phong. Ban la DO, di truoc.")
            End If
        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New XiangqiGame()
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then ShowGamePanel()
            ClearSelection() : RefreshUI() : AppendLog(game.LastLog)
            CheckAndShowGameOver()
        ElseIf line.StartsWith("MOVEREQ:") Then
            If isHost Then
                Dim parts As String() = line.Substring(8).Split(":"c)
                If parts.Length >= 5 Then
                    Dim p, fr, fc, tr, tc As Integer
                    Integer.TryParse(parts(0), p)
                    Integer.TryParse(parts(1), fr)
                    Integer.TryParse(parts(2), fc)
                    Integer.TryParse(parts(3), tr)
                    Integer.TryParse(parts(4), tc)
                    Dim errMsg As String = ""
                    If game.TryMove(p, fr, fc, tr, tc, errMsg) Then
                        AppendLog(game.LastLog) : BroadcastState() : CheckAndShowGameOver()
                    Else
                        AppendLog("Loi: " & errMsg)
                    End If
                    RefreshUI()
                End If
            End If
        ElseIf line.StartsWith("CHAT:") Then
            Dim rest As String = line.Substring(5)
            Dim sepIdx As Integer = rest.IndexOf(":"c)
            If sepIdx > 0 Then
                Dim tag As String = rest.Substring(0, sepIdx)
                Dim msg As String = rest.Substring(sepIdx + 1)
                AppendChat(tag & ": " & msg)
            End If
        End If
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False : pnlGame.Visible = True
        lblYouAre.Text = "Ban la: " & If(localPlayer = XiangqiGame.RED, "DO (di truoc)", "DEN")
        lblYouAre.ForeColor = If(localPlayer = XiangqiGame.RED, Color.FromArgb(180, 30, 30), Color.FromArgb(30, 60, 140))
        BuildCellCenters()
        ClearSelection() : RefreshUI()
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then Return
        game.ResetBoard()
        ClearSelection() : RefreshUI()
        AppendLog("Bat dau lai.") : BroadcastState()
    End Sub

    Private Sub RefreshUI()
        If game Is Nothing Then Return
        Dim myTurn As Boolean = (Not game.GameOver) AndAlso (game.CurrentPlayer = localPlayer)
        If game.GameOver Then
            lblTurn.Text = "Ket thuc game!"
            lblTurn.ForeColor = Color.DarkRed
        ElseIf myTurn Then
            Dim chk As String = If(game.IsKingInCheck(localPlayer), " (TUONG bi CHIEU!)", "")
            lblTurn.Text = "LUOT CUA BAN" & chk & Chr(10) & "Click quan roi click o di"
            lblTurn.ForeColor = Color.DarkGreen
        Else
            lblTurn.Text = "Luot cua doi thu..."
            lblTurn.ForeColor = Color.Gray
        End If
        boardPanel.Invalidate()
        RefreshPlayerCards()
    End Sub

    ''' <summary>Cap nhat trang thai hien tren 2 card "Do/Den": ai dang di, ai la Ban, ai dang bi chieu.</summary>
    Private Sub RefreshPlayerCards()
        Dim c As Integer
        For c = 0 To 1
            If lblCardStatus(c) Is Nothing Then Continue For
            Dim parts As New List(Of String)()
            If c = localPlayer Then parts.Add("Ban")
            If game IsNot Nothing Then
                If game.GameOver Then
                    parts.Add("ket thuc")
                ElseIf game.CurrentPlayer = c Then
                    parts.Add("dang di")
                Else
                    parts.Add("dang cho")
                End If
                If game.IsKingInCheck(c) Then parts.Add("dang bi CHIEU")
            End If
            lblCardStatus(c).Text = String.Join(" - ", parts)
            pnlPlayers(c).BorderStyle = If(game IsNot Nothing AndAlso Not game.GameOver AndAlso game.CurrentPlayer = c, BorderStyle.Fixed3D, BorderStyle.FixedSingle)
        Next c
    End Sub

    Private Sub CheckAndShowGameOver()
        If game IsNot Nothing AndAlso game.GameOver Then
            MessageBox.Show(game.LastLog, "Ket thuc!")
        End If
    End Sub

    ''' <summary>Log he thong (di quan, an quan, loi...) duoc gop chung vao khung chat,
    ''' co tien to "⚙" de phan biet voi tin nhan chat cua nguoi choi.</summary>
    Private Sub AppendLog(msg As String)
        AppendChat("⚙ " & msg)
    End Sub

End Class
