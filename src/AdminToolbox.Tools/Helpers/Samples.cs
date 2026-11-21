// SPDX-License-Identifier: MIT
namespace AdminToolbox.Tools.Helpers;

/// <summary>Fictitious examples. All names and addresses use reserved example domains and documentation IP ranges.</summary>
public static class Samples
{
    public const string Header = """
    Return-Path: <bounce@mailer.example.net>
    Received: from mx-in.example.org (mx-in.example.org [192.0.2.25])
            by mailbox.example.org with LMTP id 7Hk2aQ
            for <kim@example.org>; Thu, 19 Nov 2026 08:14:31 +0100
    Authentication-Results: mx-in.example.org;
           dkim=pass header.d=mailer.example.net header.s=k1;
           spf=pass smtp.mailfrom=bounce@mailer.example.net;
           dmarc=fail (p=quarantine) header.from=shop.example.com
    Received: from out.mailer.example.net (out.mailer.example.net [198.51.100.7])
            by mx-in.example.org with ESMTPS id 4XyZ;
            Thu, 19 Nov 2026 08:14:30 +0100
    Received: from app01.internal.example.net ([10.20.0.15])
            by out.mailer.example.net with ESMTP id 9Ab;
            Thu, 19 Nov 2026 07:02:11 +0000
    X-Spam-Status: No, score=2.9 required=5.0
    X-Spam-Flag: NO
    From: "Beispiel Shop" <service@shop.example.com>
    Reply-To: support@help-desk.example
    To: kim@example.org
    Subject: Ihre Bestellung 4711
    Date: Thu, 19 Nov 2026 08:02:09 +0100
    Message-ID: <4711.abc@mailer.example.net>
    """;

    public const string Zone = """
    $ORIGIN example.com.
    $TTL 3600
    @      IN  NS     ns1.example.net.
    @      IN  NS     ns2.example.net.
    @      IN  A      192.0.2.10
    @      IN  MX     10 mail.example.com.
    @      IN  TXT    "v=spf1 mx -all"
    _dmarc IN  TXT    "v=DMARC1; p=quarantine; rua=mailto:dmarc@example.com"
    mail   IN  A      192.0.2.20
    www    IN  CNAME  example.com.
    """;
}
