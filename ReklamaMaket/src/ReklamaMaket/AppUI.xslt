<?xml version="1.0"?>
<!--
  Reklama Maket — CorelDRAW paneli (docker) va menyu buyruqlarini e'lon qiladi.
  Bu fayl Addons\ReklamaMaket papkasida turishi kerak.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <!-- Hamma narsani o'zgarishsiz nusxalash -->
  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <!-- Asosiy menyudagi "Reklama" bo'limi -->
      <itemData guid="7711b833-93ff-468c-9c89-ec2cff226a5a" noBmpOnMenu="true"
                type="flyout"
                dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"
                userCaption="Reklama"
                enable="true"
                flyoutBarRef="b61f72cb-d237-4f8a-9444-845ab4740cbe"/>

      <!-- Panelni ochib/yopuvchi tugma -->
      <itemData guid="dbac5bef-29c2-4fba-a4f9-79809933b8c0" noBmpOnMenu="true"
                type="checkButton"
                check="*Docker('9deaac61-19db-4ab6-a971-86b599cace58')"
                dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"
                userCaption="Reklama Maket"
                enable="true"/>

      <!-- Panel ichidagi WPF boshqaruv elementi -->
      <itemData guid="6bf40612-88d2-41ab-933d-d32ac573bf71"
                type="wpfhost"
                hostedType="Addons\ReklamaMaket\ReklamaMaket.dll,ReklamaMaket.DockerUI"
                enable="true"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <commandBarData guid="b61f72cb-d237-4f8a-9444-845ab4740cbe"
                      type="menu"
                      nonLocalizableName="Reklama"
                      flyout="true">
        <menu>
          <item guidRef="dbac5bef-29c2-4fba-a4f9-79809933b8c0"/>
        </menu>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <dockerData guid="9deaac61-19db-4ab6-a971-86b599cace58"
                  userCaption="Reklama Maket"
                  wantReturn="true"
                  focusStyle="noThrow">
        <container>
          <item dock="fill" margin="0,0,0,0" guidRef="6bf40612-88d2-41ab-933d-d32ac573bf71"/>
        </container>
      </dockerData>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
