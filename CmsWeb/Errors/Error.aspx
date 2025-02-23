﻿<% Response.StatusCode = 500; %>
<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System.Security.Cryptography" %>
<%@ Import Namespace="System.Threading" %>
<script runat="server">
   void Page_Load() {
      byte[] delay = new byte[1];
      RandomNumberGenerator prng = new RNGCryptoServiceProvider();

      prng.GetBytes(delay);
      Thread.Sleep((int)delay[0]);
        
      IDisposable disposable = prng as IDisposable;
      if (disposable != null) { disposable.Dispose(); }
   }
</script>
<!DOCTYPE html>
<html lang="en">
<head>
    <link rel="shortcut icon" href="/favicon.ico?v=3">
    <meta charset="utf-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, maximum-scale=1.0">
    <title>500</title>
    <link href="//fonts.googleapis.com/css?family=Open+Sans:300italic,400italic,400,600,300,700" rel="stylesheet">
    <link href="//netdna.bootstrapcdn.com/font-awesome/4.3.0/css/font-awesome.css" rel="stylesheet">
    <link href="/Content/touchpoint/css/error.css" rel="stylesheet" />
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!--[if lt IE 9]>
        <script src="https://oss.maxcdn.com/html5shiv/3.7.2/html5shiv.min.js"></script>
        <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->
</head>
<body class="page-500">
    <div class="header">
        <a href="/">
            <div><img class="logo" src="/Content/touchpoint/img/logo_sm.png" alt="" style="margin-top: -4px;"></div>&nbsp;
            <strong>TouchPoint</strong> Software
        </a>
    </div>
    <div class="error-code">OOPS!</div>
    <div class="error-text">
        <span class="oops">ERROR</span><br/>
        <span class="hr"></span>
        <br>
        SOMETHING IS NOT QUITE RIGHT<br/>
        <%=HttpContextFactory.Current.Items["error"] %>
        <br>
        <span class="solve">It seems you stumbled on something that doesn't work very well. If this problem persists, please kindly let us know.
            Thanks!</span>
    </div>
    <div id="footer" class="container-fluid hidden-print">
        <div class="text-center">
            &copy; <%=DateTime.Today.Year %> <%=Resource1.CompanyName %>
        </div>
    </div>
    <script src='//ajax.googleapis.com/ajax/libs/jquery/1.11.2/jquery.min.js'></script>
    <script src="//maxcdn.bootstrapcdn.com/bootstrap/3.3.2/js/bootstrap.min.js"></script>
</body>
</html>
