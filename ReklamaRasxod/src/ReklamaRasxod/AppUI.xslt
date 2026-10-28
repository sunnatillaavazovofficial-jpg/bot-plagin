<?xml version="1.0"?>
<!--
  Reklama Rasxod — CorelDRAW paneli (docker) va menyu buyruqlarini e'lon qiladi.
  Bu fayl Addons\ReklamaRasxod papkasida turishi kerak.
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

      <!-- Asosiy menyudagi "Rasxod" bo'limi -->
      <itemData guid="1b75cd41-3411-472d-9769-42b911e4ce25" noBmpOnMenu="true"
                type="flyout"
                dynamicCategory="597f7de1-9e1e-4f03-8bf9-e53ea72a0687"
                userCaption="Rasxod"
                enable="true"
                flyoutBarRef="0ef9091e-ad10-4e8a-ac32-293b11f50bb8"/>

      <!-- Panelni ochib/yopuvchi tugma -->
      <itemData guid="3edbfa85-fa48-42c6-8284-cab530e86c32" noBmpOnMenu="true"
                type="checkButton"
                check="*Docker('531aa87c-db8d-43e7-a91e-4b4e0e77e7ea')"
                dynamicCategory="597f7de1-9e1e-4f03-8bf9-e53ea72a0687"
                userCaption="Reklama Rasxod"
                enable="true"/>

      <!-- Panel ichidagi WPF boshqaruv elementi -->
      <itemData guid="12944e53-451f-4b8a-af0e-429a7282d55a"
                type="wpfhost"
                hostedType="Addons\ReklamaRasxod\ReklamaRasxod.dll,ReklamaRasxod.DockerUI"
                enable="true"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <commandBarData guid="0ef9091e-ad10-4e8a-ac32-293b11f50bb8"
                      type="menu"
                      nonLocalizableName="Rasxod"
                      flyout="true">
        <menu>
          <item guidRef="3edbfa85-fa48-42c6-8284-cab530e86c32"/>
        </menu>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <dockerData guid="531aa87c-db8d-43e7-a91e-4b4e0e77e7ea"
                  userCaption="Reklama Rasxod"
                  wantReturn="true"
                  focusStyle="noThrow">
        <container>
          <item dock="fill" margin="0,0,0,0" guidRef="12944e53-451f-4b8a-af0e-429a7282d55a"/>
        </container>
      </dockerData>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
