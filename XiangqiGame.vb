Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

''' <summary>
''' Logic Co Tuong (Xiangqi) day du cho PvP online.
''' Ban co: 9 cot (0..8), 10 hang (0..9).
''' Quy uoc:
'''   Board(row, col), row=0..9 (hang), col=0..8 (cot).
'''   Do = nguoi choi 0 (Host, di truoc), vi tri hang 0..4 (ben Do).
'''   Den = nguoi choi 1 (Khach), vi tri hang 5..9 (ben Den).
'''   Gia tri o Board:
'''     0  = trong
'''    +N  = quan Do (N = loai quan)
'''    -N  = quan Den
'''
''' Loai quan:
'''   PT_TUONG (General/King)  = 1
'''   PT_SI    (Advisor)       = 2
'''   PT_TUONG_VOI (Elephant)  = 3
'''   PT_MA    (Horse)         = 4
'''   PT_XE    (Chariot)       = 5
'''   PT_PHAO  (Cannon)        = 6
'''   PT_TOT   (Soldier)       = 7
''' </summary>
Public Class XiangqiGame

    Public Const RED As Integer = 0    ' Red / Host (di truoc)
    Public Const BLK As Integer = 1   ' Black / Client

    Public Const PT_TUONG As Integer = 1       ' Tuong/Tuong quan (General)
    Public Const PT_SI As Integer = 2          ' Si (Advisor)
    Public Const PT_VOI As Integer = 3         ' Voi/Tuong (Elephant)
    Public Const PT_MA As Integer = 4          ' Ma (Horse)
    Public Const PT_XE As Integer = 5          ' Xe (Chariot)
    Public Const PT_PHAO As Integer = 6        ' Phao (Cannon)
    Public Const PT_TOT As Integer = 7         ' Tot (Soldier/Pawn)

    Public Structure XqMove
        Public FromRow As Integer
        Public FromCol As Integer
        Public ToRow As Integer
        Public ToCol As Integer
    End Structure

    Public Board(9, 8) As Integer  ' Board(row, col)
    Public CurrentPlayer As Integer
    Public GameOver As Boolean
    Public LastLog As String
    Public CapturedByDo As List(Of Integer)
    Public CapturedByDen As List(Of Integer)

    Public Sub New()
        ResetBoard()
    End Sub

    Public Sub ResetBoard()
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                Board(r, c) = 0
            Next c
        Next r

        ' --- Quan Den (hang 0..4, hang tren man hinh) ---
        ' hang 0: Xe Si Voi Si Tuong Si Voi Si Xe (col 0..8)
        Board(0, 0) = -PT_XE   : Board(0, 8) = -PT_XE
        Board(0, 1) = -PT_MA   : Board(0, 7) = -PT_MA
        Board(0, 2) = -PT_VOI  : Board(0, 6) = -PT_VOI
        Board(0, 3) = -PT_SI   : Board(0, 5) = -PT_SI
        Board(0, 4) = -PT_TUONG
        ' hang 2: Phao
        Board(2, 1) = -PT_PHAO : Board(2, 7) = -PT_PHAO
        ' hang 3: Tot
        Dim tc As Integer
        For tc = 0 To 8 Step 2
            Board(3, tc) = -PT_TOT
        Next tc

        ' --- Quan Do (hang 5..9, hang duoi man hinh) ---
        Board(9, 0) = PT_XE    : Board(9, 8) = PT_XE
        Board(9, 1) = PT_MA    : Board(9, 7) = PT_MA
        Board(9, 2) = PT_VOI   : Board(9, 6) = PT_VOI
        Board(9, 3) = PT_SI    : Board(9, 5) = PT_SI
        Board(9, 4) = PT_TUONG
        Board(7, 1) = PT_PHAO  : Board(7, 7) = PT_PHAO
        Dim rc As Integer
        For rc = 0 To 8 Step 2
            Board(6, rc) = PT_TOT
        Next rc

        CurrentPlayer = RED
        GameOver = False
        CapturedByDo = New List(Of Integer)()
        CapturedByDen = New List(Of Integer)()
        LastLog = "Bat dau game moi. Do di truoc."
    End Sub

    ' ============================================================
    '  TRUY VAN CO BAN
    ' ============================================================
    Public Function GetPieceAt(row As Integer, col As Integer) As Integer
        Return Board(row, col)
    End Function

    Public Function PieceColorAt(row As Integer, col As Integer) As Integer
        Return ColorOf(Board(row, col))
    End Function

    Public Function IsKingInCheck(color As Integer) As Boolean
        Return IsKingInCheckOnBoard(Board, color)
    End Function

    Public Function HasAnyLegalMove(color As Integer) As Boolean
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                Dim p As Integer = Board(r, c)
                If p <> 0 AndAlso ColorOf(p) = color Then
                    If GetLegalMoves(r, c).Count > 0 Then Return True
                End If
            Next c
        Next r
        Return False
    End Function

    ''' <summary>Ten hien thi quan theo mau (Do/Den).</summary>
    Public Shared Function GetGlyphName(pieceVal As Integer) As String
        If pieceVal = 0 Then Return ""
        Dim isDo As Boolean = (pieceVal > 0)
        Dim pt As Integer = Math.Abs(pieceVal)
        Select Case pt
            Case PT_TUONG : Return If(isDo, "帅", "将")
            Case PT_SI    : Return If(isDo, "仕", "士")
            Case PT_VOI   : Return If(isDo, "相", "象")
            Case PT_MA    : Return If(isDo, "馬", "馬")
            Case PT_XE    : Return If(isDo, "俥", "車")
            Case PT_PHAO  : Return If(isDo, "炮", "砲")
            Case PT_TOT   : Return If(isDo, "兵", "卒")
            Case Else : Return ""
        End Select
    End Function

    Public Function GetLegalMoves(row As Integer, col As Integer) As List(Of XqMove)
        Dim result As New List(Of XqMove)()
        Dim p As Integer = Board(row, col)
        If p = 0 Then Return result
        Dim color As Integer = ColorOf(p)
        Dim pseudo As List(Of XqMove) = GeneratePseudoMoves(row, col)
        Dim mv As XqMove
        For Each mv In pseudo
            If IsMoveLegal(mv, color) Then result.Add(mv)
        Next
        Return result
    End Function

    ' ============================================================
    '  THUC HIEN NUOC DI
    ' ============================================================
    Public Function TryMove(player As Integer, fromRow As Integer, fromCol As Integer,
                            toRow As Integer, toCol As Integer, ByRef errorMsg As String) As Boolean
        errorMsg = ""
        If GameOver Then errorMsg = "Game da ket thuc." : Return False
        If player <> CurrentPlayer Then errorMsg = "Khong phai luot cua ban." : Return False
        If Not InBounds(fromRow, fromCol) OrElse Not InBounds(toRow, toCol) Then
            errorMsg = "Toa do khong hop le." : Return False
        End If

        Dim p As Integer = Board(fromRow, fromCol)
        If p = 0 Then errorMsg = "O nay khong co quan." : Return False
        If ColorOf(p) <> player Then errorMsg = "Ban chi duoc di quan cua minh." : Return False

        Dim legalMoves As List(Of XqMove) = GetLegalMoves(fromRow, fromCol)
        Dim found As Boolean = False
        Dim chosen As XqMove
        Dim mv As XqMove
        For Each mv In legalMoves
            If mv.ToRow = toRow AndAlso mv.ToCol = toCol Then
                chosen = mv : found = True : Exit For
            End If
        Next
        If Not found Then errorMsg = "Nuoc di khong hop le." : Return False

        Dim capturedPiece As Integer = Board(toRow, toCol)
        ApplyMoveToBoard(Board, chosen)

        If capturedPiece <> 0 Then
            If player = RED Then
                CapturedByDo.Add(Math.Abs(capturedPiece))
            Else
                CapturedByDen.Add(Math.Abs(capturedPiece))
            End If
        End If

        Dim sideName As String = If(player = RED, "Do", "Den")
        Dim msg As String = sideName & " di " & PieceName(Math.Abs(p)) &
                            " " & SqName(fromRow, fromCol) & "-" & SqName(toRow, toCol)
        If capturedPiece <> 0 Then
            msg &= " (an " & PieceName(Math.Abs(capturedPiece)) & ")"
        End If

        CurrentPlayer = 1 - player

        Dim oppInCheck As Boolean = IsKingInCheck(CurrentPlayer)
        Dim oppHasMoves As Boolean = HasAnyLegalMove(CurrentPlayer)

        If Not oppHasMoves Then
            GameOver = True
            If oppInCheck Then
                msg &= " - Chieu het! " & sideName & " thang!"
            Else
                msg &= " - Pat (hoa)!"
            End If
        ElseIf oppInCheck Then
            msg &= " - Chieu!"
        End If

        LastLog = msg
        Return True
    End Function

    ' ============================================================
    '  SINH NUOC DI (pseudo)
    ' ============================================================
    Private Function GeneratePseudoMoves(row As Integer, col As Integer) As List(Of XqMove)
        Dim moves As New List(Of XqMove)()
        Dim p As Integer = Board(row, col)
        If p = 0 Then Return moves
        Dim color As Integer = ColorOf(p)
        Dim pt As Integer = Math.Abs(p)

        Select Case pt
            Case PT_TUONG : AddTuongMoves(moves, row, col, color)
            Case PT_SI    : AddSiMoves(moves, row, col, color)
            Case PT_VOI   : AddVoiMoves(moves, row, col, color)
            Case PT_MA    : AddMaMoves(moves, row, col, color)
            Case PT_XE    : AddXeMoves(moves, row, col, color)
            Case PT_PHAO  : AddPhaoMoves(moves, row, col, color)
            Case PT_TOT   : AddTotMoves(moves, row, col, color)
        End Select
        Return moves
    End Function

    ' --- Tuong (General): chi di trong cung 3x3, khong duoc de mat doi ---
    Private Sub AddTuongMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        Dim dirs(,) As Integer = {{1, 0}, {-1, 0}, {0, 1}, {0, -1}}
        Dim i As Integer
        For i = 0 To 3
            Dim nr As Integer = row + dirs(i, 0)
            Dim nc As Integer = col + dirs(i, 1)
            If InPalace(nr, nc, color) Then
                Dim t As Integer = Board(nr, nc)
                If t = 0 OrElse ColorOf(t) <> color Then
                    AddMove(moves, row, col, nr, nc)
                End If
            End If
        Next i
    End Sub

    ' --- Si (Advisor): di cheo 1 buoc trong cung ---
    Private Sub AddSiMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        Dim dirs(,) As Integer = {{1, 1}, {1, -1}, {-1, 1}, {-1, -1}}
        Dim i As Integer
        For i = 0 To 3
            Dim nr As Integer = row + dirs(i, 0)
            Dim nc As Integer = col + dirs(i, 1)
            If InPalace(nr, nc, color) Then
                Dim t As Integer = Board(nr, nc)
                If t = 0 OrElse ColorOf(t) <> color Then
                    AddMove(moves, row, col, nr, nc)
                End If
            End If
        Next i
    End Sub

    ' --- Voi (Elephant): di cheo 2 buoc, khong qua song, khong bi chan ---
    Private Sub AddVoiMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        ' Voi di cheo 2 o (tuong tu ma nhung chi cheo), bi chan neu o giua co quan
        Dim dirs(,) As Integer = {{2, 2}, {2, -2}, {-2, 2}, {-2, -2}}
        Dim blocks(,) As Integer = {{1, 1}, {1, -1}, {-1, 1}, {-1, -1}}
        Dim i As Integer
        For i = 0 To 3
            Dim nr As Integer = row + dirs(i, 0)
            Dim nc As Integer = col + dirs(i, 1)
            Dim br As Integer = row + blocks(i, 0)
            Dim bc As Integer = col + blocks(i, 1)
            If InBounds(nr, nc) AndAlso InOwnHalf(nr, color) AndAlso Board(br, bc) = 0 Then
                Dim t As Integer = Board(nr, nc)
                If t = 0 OrElse ColorOf(t) <> color Then
                    AddMove(moves, row, col, nr, nc)
                End If
            End If
        Next i
    End Sub

    ' --- Ma (Horse): di hinh chu L, bi chan neu o ke bi chiem ---
    Private Sub AddMaMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        ' 8 nuoc di Ma: moi nuoc gom buoc "thang" truoc roi reo sang
        Dim steps(,) As Integer = {
            {1, 0, 2, 1}, {1, 0, 2, -1},
            {-1, 0, -2, 1}, {-1, 0, -2, -1},
            {0, 1, 1, 2}, {0, 1, -1, 2},
            {0, -1, 1, -2}, {0, -1, -1, -2}
        }
        Dim i As Integer
        For i = 0 To 7
            Dim blockR As Integer = row + steps(i, 0)
            Dim blockC As Integer = col + steps(i, 1)
            Dim nr As Integer = row + steps(i, 2)
            Dim nc As Integer = col + steps(i, 3)
            If InBounds(nr, nc) AndAlso InBounds(blockR, blockC) AndAlso Board(blockR, blockC) = 0 Then
                Dim t As Integer = Board(nr, nc)
                If t = 0 OrElse ColorOf(t) <> color Then
                    AddMove(moves, row, col, nr, nc)
                End If
            End If
        Next i
    End Sub

    ' --- Xe (Chariot): di thang hang/cot khong gioi han ---
    Private Sub AddXeMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        Dim dirs(,) As Integer = {{1, 0}, {-1, 0}, {0, 1}, {0, -1}}
        Dim i As Integer
        For i = 0 To 3
            Dim nr As Integer = row + dirs(i, 0)
            Dim nc As Integer = col + dirs(i, 1)
            Do While InBounds(nr, nc)
                Dim t As Integer = Board(nr, nc)
                If t = 0 Then
                    AddMove(moves, row, col, nr, nc)
                Else
                    If ColorOf(t) <> color Then AddMove(moves, row, col, nr, nc)
                    Exit Do
                End If
                nr += dirs(i, 0) : nc += dirs(i, 1)
            Loop
        Next i
    End Sub

    ' --- Phao (Cannon): di nhu Xe nhung an phai nhay qua dung 1 quan ---
    Private Sub AddPhaoMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        Dim dirs(,) As Integer = {{1, 0}, {-1, 0}, {0, 1}, {0, -1}}
        Dim i As Integer
        For i = 0 To 3
            Dim nr As Integer = row + dirs(i, 0)
            Dim nc As Integer = col + dirs(i, 1)
            Dim screen As Integer = 0  ' so quan can qua
            Do While InBounds(nr, nc)
                Dim t As Integer = Board(nr, nc)
                If screen = 0 Then
                    If t = 0 Then
                        AddMove(moves, row, col, nr, nc)  ' di khong an
                    Else
                        screen = 1  ' gap quan chan, bat dau dem
                    End If
                ElseIf screen = 1 Then
                    If t <> 0 Then
                        If ColorOf(t) <> color Then
                            AddMove(moves, row, col, nr, nc)  ' an quan dich
                        End If
                        Exit Do  ' du an duoc hay khong cung dung
                    End If
                End If
                nr += dirs(i, 0) : nc += dirs(i, 1)
            Loop
        Next i
    End Sub

    ' --- Tot (Soldier): truoc song chi di thang, qua song them 2 huong ngang ---
    Private Sub AddTotMoves(moves As List(Of XqMove), row As Integer, col As Integer, color As Integer)
        Dim forward As Integer = If(color = RED, -1, 1)  ' Do tien len (row giam), Den tien xuong (row tang)
        Dim crossed As Boolean = Not InOwnHalf(row, color)  ' da qua song

        Dim nr As Integer = row + forward
        If InBounds(nr, col) Then
            Dim t As Integer = Board(nr, col)
            If t = 0 OrElse ColorOf(t) <> color Then
                AddMove(moves, row, col, nr, col)
            End If
        End If

        If crossed Then
            Dim dc As Integer
            For dc = -1 To 1 Step 2
                Dim nc As Integer = col + dc
                If InBounds(row, nc) Then
                    Dim t As Integer = Board(row, nc)
                    If t = 0 OrElse ColorOf(t) <> color Then
                        AddMove(moves, row, col, row, nc)
                    End If
                End If
            Next dc
        End If
    End Sub

    ' ============================================================
    '  KIEM TRA AN TOAN / CHIEU
    ' ============================================================
    Private Function IsMoveLegal(mv As XqMove, color As Integer) As Boolean
        Dim temp As Integer(,) = CloneBoard()
        ApplyMoveToBoard(temp, mv)
        Return Not IsKingInCheckOnBoard(temp, color)
    End Function

    Private Function IsKingInCheckOnBoard(b As Integer(,), color As Integer) As Boolean
        ' Tim Tuong cua mau color
        Dim kr As Integer, kc As Integer
        kr = -1 : kc = -1
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                Dim p As Integer = b(r, c)
                If p <> 0 AndAlso Math.Abs(p) = PT_TUONG AndAlso ColorOf(p) = color Then
                    kr = r : kc = c
                End If
            Next c
        Next r
        If kr = -1 Then Return False

        Dim opp As Integer = 1 - color
        ' Kiem tra Tuong mat doi nhau (flying general)
        If FlyingGeneral(b, kr, kc, color) Then Return True

        ' Kiem tra cac quan dich tan cong
        For r = 0 To 9
            For c = 0 To 8
                Dim p As Integer = b(r, c)
                If p = 0 OrElse ColorOf(p) <> opp Then Continue For
                Dim pt As Integer = Math.Abs(p)
                Select Case pt
                    Case PT_XE
                        If CanXeAttack(b, r, c, kr, kc) Then Return True
                    Case PT_PHAO
                        If CanPhaoAttack(b, r, c, kr, kc) Then Return True
                    Case PT_MA
                        If CanMaAttack(r, c, kr, kc, b) Then Return True
                    Case PT_TOT
                        If CanTotAttack(r, c, kr, kc, opp) Then Return True
                End Select
            Next c
        Next r
        Return False
    End Function

    Private Function FlyingGeneral(b As Integer(,), kr As Integer, kc As Integer, color As Integer) As Boolean
        ' Tim Tuong dich tren cung cot kc
        Dim opp As Integer = 1 - color
        Dim dr As Integer = If(color = RED, -1, 1)
        Dim r As Integer = kr + dr
        Do While r >= 0 AndAlso r <= 9
            Dim p As Integer = b(r, kc)
            If p <> 0 Then
                If Math.Abs(p) = PT_TUONG AndAlso ColorOf(p) = opp Then Return True
                Return False  ' co quan can giua
            End If
            r += dr
        Loop
        Return False
    End Function

    Private Function CanXeAttack(b As Integer(,), fr As Integer, fc As Integer, tr As Integer, tc As Integer) As Boolean
        If fr <> tr AndAlso fc <> tc Then Return False
        Dim stepR As Integer = Math.Sign(tr - fr)
        Dim stepC As Integer = Math.Sign(tc - fc)
        Dim cr As Integer = fr + stepR
        Dim cc As Integer = fc + stepC
        Do While cr <> tr OrElse cc <> tc
            If b(cr, cc) <> 0 Then Return False
            cr += stepR : cc += stepC
        Loop
        Return True
    End Function

    Private Function CanPhaoAttack(b As Integer(,), fr As Integer, fc As Integer, tr As Integer, tc As Integer) As Boolean
        If fr <> tr AndAlso fc <> tc Then Return False
        Dim stepR As Integer = Math.Sign(tr - fr)
        Dim stepC As Integer = Math.Sign(tc - fc)
        Dim cr As Integer = fr + stepR
        Dim cc As Integer = fc + stepC
        Dim screen As Integer = 0
        Do While cr <> tr OrElse cc <> tc
            If b(cr, cc) <> 0 Then screen += 1
            If screen > 1 Then Return False
            cr += stepR : cc += stepC
        Loop
        Return screen = 1
    End Function

    Private Function CanMaAttack(fr As Integer, fc As Integer, tr As Integer, tc As Integer, b As Integer(,)) As Boolean
        Dim dr As Integer = tr - fr
        Dim dc As Integer = tc - fc
        ' Ma di (+-1, +-2) hoac (+-2, +-1)
        If Math.Abs(dr) = 1 AndAlso Math.Abs(dc) = 2 Then
            Return b(fr, fc + Math.Sign(dc)) = 0
        ElseIf Math.Abs(dr) = 2 AndAlso Math.Abs(dc) = 1 Then
            Return b(fr + Math.Sign(dr), fc) = 0
        End If
        Return False
    End Function

    Private Function CanTotAttack(fr As Integer, fc As Integer, tr As Integer, tc As Integer, oppColor As Integer) As Boolean
        ' Tot dich (oppColor) tan cong nhu Tot di chuyen cua no
        Dim forward As Integer = If(oppColor = RED, -1, 1)
        If fr + forward = tr AndAlso fc = tc Then Return True  ' tan cong thang
        If fr = tr AndAlso Math.Abs(fc - tc) = 1 Then
            ' chi tan cong ngang neu da qua song
            Return Not InOwnHalf(fr, oppColor)
        End If
        Return False
    End Function

    ' ============================================================
    '  TIEN ICH
    ' ============================================================
    Private Function InBounds(r As Integer, c As Integer) As Boolean
        Return r >= 0 AndAlso r <= 9 AndAlso c >= 0 AndAlso c <= 8
    End Function

    ''' <summary>O (r,c) co nam trong cung khong (cung 3x3, cot 3-5)?</summary>
    Private Function InPalace(r As Integer, c As Integer, color As Integer) As Boolean
        If c < 3 OrElse c > 5 Then Return False
        If color = RED Then Return r >= 7 AndAlso r <= 9
        Return r >= 0 AndAlso r <= 2
    End Function

    ''' <summary>O (r, _) co trong nua san nha cua color khong (Voi khong qua song)?</summary>
    Private Function InOwnHalf(r As Integer, color As Integer) As Boolean
        If color = RED Then Return r >= 5
        Return r <= 4
    End Function

    Private Function ColorOf(p As Integer) As Integer
        If p = 0 Then Return -1
        If p > 0 Then Return RED
        Return BLK
    End Function

    Private Sub AddMove(moves As List(Of XqMove), fr As Integer, fc As Integer, tr As Integer, tc As Integer)
        Dim mv As XqMove
        mv.FromRow = fr : mv.FromCol = fc : mv.ToRow = tr : mv.ToCol = tc
        moves.Add(mv)
    End Sub

    Private Function CloneBoard() As Integer(,)
        Dim b(9, 8) As Integer
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                b(r, c) = Board(r, c)
            Next c
        Next r
        Return b
    End Function

    Private Sub ApplyMoveToBoard(b As Integer(,), mv As XqMove)
        b(mv.ToRow, mv.ToCol) = b(mv.FromRow, mv.FromCol)
        b(mv.FromRow, mv.FromCol) = 0
    End Sub

    Private Function PieceName(pt As Integer) As String
        Select Case pt
            Case PT_TUONG : Return "Tuong"
            Case PT_SI    : Return "Si"
            Case PT_VOI   : Return "Voi"
            Case PT_MA    : Return "Ma"
            Case PT_XE    : Return "Xe"
            Case PT_PHAO  : Return "Phao"
            Case PT_TOT   : Return "Tot"
            Case Else : Return "?"
        End Select
    End Function

    Private Function SqName(row As Integer, col As Integer) As String
        Return Chr(Asc("a"c) + col) & (10 - row).ToString()
    End Function

    ' ============================================================
    '  SERIALIZE / DESERIALIZE
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                sb.Append(Board(r, c).ToString())
                sb.Append(",")
            Next c
        Next r
        sb.Length -= 1

        sb.Append("|")
        sb.Append(CurrentPlayer.ToString())

        sb.Append("|")
        sb.Append(If(GameOver, "1", "0"))

        sb.Append("|")
        Dim i As Integer
        For i = 0 To CapturedByDo.Count - 1
            sb.Append(CapturedByDo(i).ToString())
            If i < CapturedByDo.Count - 1 Then sb.Append(",")
        Next i

        sb.Append("|")
        For i = 0 To CapturedByDen.Count - 1
            sb.Append(CapturedByDen(i).ToString())
            If i < CapturedByDen.Count - 1 Then sb.Append(",")
        Next i

        sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)

        Dim boardParts As String() = parts(0).Split(","c)
        Dim idx As Integer = 0
        Dim r As Integer, c As Integer
        For r = 0 To 9
            For c = 0 To 8
                Board(r, c) = Integer.Parse(boardParts(idx))
                idx += 1
            Next c
        Next r

        CurrentPlayer = Integer.Parse(parts(1))
        GameOver = (parts(2) = "1")

        CapturedByDo = New List(Of Integer)()
        If parts(3).Length > 0 Then
            Dim cw As String() = parts(3).Split(","c)
            Dim k As Integer
            For k = 0 To cw.Length - 1
                CapturedByDo.Add(Integer.Parse(cw(k)))
            Next k
        End If

        CapturedByDen = New List(Of Integer)()
        If parts(4).Length > 0 Then
            Dim cb As String() = parts(4).Split(","c)
            Dim k2 As Integer
            For k2 = 0 To cb.Length - 1
                CapturedByDen.Add(Integer.Parse(cb(k2)))
            Next k2
        End If

        If parts.Length >= 6 Then LastLog = parts(5)
    End Sub

End Class
