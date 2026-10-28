<?xml version="1.0"?>
<!--
  Ish maydoniga (workspace) "Rasxod" menyusini va standart asboblar paneliga
  "Reklama Rasxod" tugmasini qo'shadi. Har bir workspace uchun bir marta bajariladi:
  o'zgartirgandan keyin CorelDRAW'ni F8 bosib turgan holda ishga tushiring.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:frmwrk="Corel Framework Data"
                exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <!-- Asosiy menyu satri -->
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']"/>
    <!-- "Rasxod" menyusi -->
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='0ef9091e-ad10-4e8a-ac32-293b11f50bb8']"/>
    <!-- Standart asboblar paneli -->
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']"/>
    <frmwrk:compositeNode xPath="/uiConfig/frame"/>
  </frmwrk:uiconfig>

  <!-- Hamma narsani o'zgarishsiz nusxalash -->
  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <!-- Menyu/asboblar paneliga element qo'shish uchun yordamchi -->
  <xsl:template match="node()|@*" mode="insert-item">
    <xsl:param name="after"></xsl:param>
    <xsl:param name="content"></xsl:param>
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:for-each select="node()">
        <xsl:copy>
          <xsl:apply-templates select="node()|@*"/>
        </xsl:copy>
        <xsl:if test="name()='item' and @guidRef=$after">
          <xsl:copy-of select="$content"/>
        </xsl:if>
      </xsl:for-each>
      <xsl:if test="not(./item[@guidRef=$after])">
        <xsl:copy-of select="$content"/>
      </xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- Asosiy menyuga "Rasxod" bo'limini qo'shish -->
  <xsl:template match="commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']/menu">
    <xsl:apply-templates mode="insert-item" select=".">
      <xsl:with-param name="content">
        <xsl:if test="not(./item[@guidRef='1b75cd41-3411-472d-9769-42b911e4ce25'])">
          <item guidRef="1b75cd41-3411-472d-9769-42b911e4ce25"/>
        </xsl:if>
      </xsl:with-param>
      <xsl:with-param name="after" select="'47cc9a2d-0b7a-4df1-a686-ea3aa21b4631'"/>
    </xsl:apply-templates>
  </xsl:template>

  <!-- Standart asboblar panelining oxiriga "Reklama Rasxod" tugmasi -->
  <xsl:template match="commandBarData[@guid='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']/toolbar">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='3edbfa85-fa48-42c6-8284-cab530e86c32'])">
        <item guidRef="3edbfa85-fa48-42c6-8284-cab530e86c32"/>
      </xsl:if>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
