Imports System.Net
Imports System.IO
Imports Newtonsoft.Json.Linq
Imports Newtonsoft.Json
Imports System.Data.SqlClient


Public Class Main


    Dim icount As Integer
    Dim DBChainHeight, NodeChainHeight As Integer
    Dim FirstLoad As Boolean = True

    Public Function GetConnStr() As String

        Return Environment.GetEnvironmentVariable("SQLConnStrTabbyPOS")

    End Function

    Public Function GetNodeURL() As String

        Return Environment.GetEnvironmentVariable("ErgoNodeURL")

    End Function

    Public Function GetNodeAPIKey() As String

        Return Environment.GetEnvironmentVariable("NodeAPIKey")

    End Function

    Private Sub Main_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        RefreshGrid()
    End Sub

    Private Function INSERT2TxNode( _
        ByVal ChainHeight As Integer, ByVal TxID As String, ByVal BoxID As String, ByVal WalletAddress As String, _
        ByVal Amount As Decimal, ByVal Confirmed As Integer) As Boolean

        Dim gCnnPCE As SqlConnection
        gCnnPCE = New SqlConnection(GetConnStr)
        Try
            gCnnPCE.Open()
            Dim str As String
            Dim cmd As New SqlCommand(str, gCnnPCE)

            str = "SELECT ID FROM TxNode WHERE TxID = @TxID"
            cmd.CommandText = str
            cmd.Parameters.AddWithValue("@TxID", TxID)

            Dim da As New SqlDataAdapter(cmd)
            Dim dt As New DataTable
            da.Fill(dt)
            If dt.Rows.Count > 0 Then
                str = "UPDATE TxNode " & _
                     " SET Confirmed = @Confirmed" & _
                     " WHERE TxID = @TxID"

                cmd.CommandText = str
                cmd.Parameters.AddWithValue("@ChainHeight", ChainHeight)
                cmd.Parameters.AddWithValue("@BoxID", BoxID)
                cmd.Parameters.AddWithValue("@WalletAddress", WalletAddress)
                cmd.Parameters.AddWithValue("@Amount", Amount)
                cmd.Parameters.AddWithValue("@Confirmed", Confirmed)

                If cmd.ExecuteNonQuery() > 0 Then
                    Return True
                Else
                    Return False
                End If

            Else
                str = "INSERT INTO TxNode(" & _
                                     "ChainHeight, TxID, BoxID, WalletAddress, Amount, Confirmed) VALUES ( " & _
                                     "@ChainHeight, @TxID, @BoxID, @WalletAddress, @Amount, @Confirmed) "

                cmd.CommandText = str
                cmd.Parameters.AddWithValue("@ChainHeight", ChainHeight)
                cmd.Parameters.AddWithValue("@BoxID", BoxID)
                cmd.Parameters.AddWithValue("@WalletAddress", WalletAddress)
                cmd.Parameters.AddWithValue("@Amount", Amount)
                cmd.Parameters.AddWithValue("@Confirmed", Confirmed)

                If cmd.ExecuteNonQuery() > 0 Then
                    Return True
                Else
                    Return False
                End If

            End If

        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        Finally
            gCnnPCE.Close()
            gCnnPCE.Dispose()
        End Try



    End Function

    Private Function GetChainTx2DB(ByVal MinChainHeight As Integer) As Integer

        Dim strReq As String
        strReq = GetNodeURL() & "/wallet/transactions?minInclusionHeight=" & MinChainHeight
        Dim req As WebRequest = WebRequest.Create((strReq))
        req.Method = "GET"
        req.Headers("api_key") = GetNodeAPIKey()
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
        Try
            Dim resp As HttpWebResponse = CType(req.GetResponse, HttpWebResponse)
            If (resp.StatusCode = HttpStatusCode.OK) Then
                Dim dataStream As Stream = resp.GetResponseStream
                Dim reader As StreamReader = New StreamReader(dataStream)
                Dim json As String
                json = reader.ReadToEnd
                RichTextBox1.Text = json

                Dim data = JsonConvert.DeserializeObject(Of List(Of Object))(json)
                Dim _ChainHeight As Integer
                Dim _TxID As String
                Dim _BoxID As String
                Dim _WalletAddress As String
                Dim _Amount As Decimal
                Dim _Confirmed As Integer
                Dim count As Integer = 0

                For Each item In data
                    _ChainHeight = item("outputs")(0)("creationHeight")
                    _TxID = item("id")
                    _BoxID = item("outputs")(0)("boxId")
                    _WalletAddress = item("outputs")(0)("address")
                    _Amount = item("outputs")(0)("value") / 1000000000
                    _Confirmed = item("numConfirmations")
                    count = count + 1
                    INSERT2TxNode(_ChainHeight, _TxID, _BoxID, _WalletAddress, _Amount, _Confirmed)
                Next

                If count > 0 Then
                    Return True
                Else
                    Return False
                End If

            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        End Try
    End Function

 
    Private Function GetDBLastHeight()
        Dim gCnnPCE As SqlConnection
        gCnnPCE = New SqlConnection(GetConnStr)
        Try

            gCnnPCE.Open()
            Dim str As String
            If FirstLoad = True Then
                str = "SELECT ISNULL(MAX(ChainHeight),0) FROM TxNode"
            Else
                str = "SELECT ISNULL(MAX(ChainHeight),0) FROM TxNode WHERE Confirmed = 0"
            End If

            Dim cmd As New SqlCommand(str, gCnnPCE)
            FirstLoad = False
            Return cmd.ExecuteScalar

        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        Finally
            gCnnPCE.Close()
            gCnnPCE.Dispose()
        End Try
    End Function

    Private Function GetCurrentChainHeight() As Integer
        Dim strReq As String
        strReq = GetNodeURL() & "/blockchain/indexedHeight"
        Dim req As WebRequest = WebRequest.Create((strReq))
        req.Method = "GET"
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
        Try
            Dim resp As HttpWebResponse = CType(req.GetResponse, HttpWebResponse)
            If (resp.StatusCode = HttpStatusCode.OK) Then
                Dim dataStream As Stream = resp.GetResponseStream
                Dim reader As StreamReader = New StreamReader(dataStream)
                Dim Result As String
                Result = reader.ReadToEnd
                RichTextBox1.Text = Result
                Dim json As JObject = JObject.Parse(Result)
                Return json("fullHeight")

            End If
        Catch ex As Exception

        End Try
    End Function

    Private Sub btnGetHeight_Click(sender As Object, e As EventArgs) Handles btnGetHeight.Click
        MsgBox(GetCurrentChainHeight, MsgBoxStyle.Information)
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick

        Timer1.Enabled = False

        Try
            DBChainHeight = GetDBLastHeight()
            NodeChainHeight = GetCurrentChainHeight()

            If DBChainHeight = 0 Then
                DBChainHeight = NodeChainHeight
            End If

            If GetChainTx2DB(DBChainHeight) = True Then
                'Refresh Grid
                Me.BeginInvoke(New EventHandler(AddressOf RefreshGrid), 1)
            End If

            'If NodeChainHeight >= DBChainHeight Then
            '    If GetChainTx2DB(DBChainHeight) = True Then
            '        'Refresh Grid
            '        Me.BeginInvoke(New EventHandler(AddressOf RefreshGrid), 1)
            '    End If
            'End If
        Catch ex As Exception
            WriteErrror("Timer1", ex.Message)
        Finally
            Timer1.Enabled = True
        End Try
    End Sub


    Private Sub RefreshGrid()
        Dim gCnnPCE As SqlConnection
        gCnnPCE = New SqlConnection(GetConnStr)

        Try
            gCnnPCE.Open()
            Dim str As String
            str = "SELECT TOP 100 * FROM TxNode ORDER BY ID DESC"
            Dim cmd As New SqlCommand(str, gCnnPCE)
            Dim da As New SqlDataAdapter(cmd)
            Dim dt As New DataTable
            da.Fill(dt)

            Me.GridControl1.DataSource = dt

        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        Finally
            gCnnPCE.Close()
            gCnnPCE.Dispose()
        End Try
    End Sub


    Private Sub Timer2_Tick(sender As Object, e As EventArgs) Handles Timer2.Tick
        If icount = 10 Then
            icount = 0
            imgGreen.SendToBack()
        Else
            imgGreen.BringToFront()
        End If
        icount = icount + 1
    End Sub


    Public Sub WriteErrror(ByVal ModuleName As String, ByVal ErrMsg As String)

        Dim gCnnPCE As SqlConnection
        gCnnPCE = New SqlConnection(GetConnStr)
        gCnnPCE.Open()

        Dim str As String
        Dim cmd As New SqlCommand(str, gCnnPCE)
        str = "INSERT INTO Errors (ModuleName, ErrMsg) VALUES (@ModuleName, @ErrMsg)"
        cmd.CommandText = str
        cmd.Parameters.AddWithValue("@ModuleName", ModuleName)
        cmd.Parameters.AddWithValue("@ErrMsg", ErrMsg)

        cmd.ExecuteNonQuery()

        gCnnPCE.Close()
        gCnnPCE.Dispose()

    End Sub

End Class
