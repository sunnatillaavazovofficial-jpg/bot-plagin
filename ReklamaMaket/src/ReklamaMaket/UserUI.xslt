<?xml version="1.0"?>
<!--
  Ish maydoniga (workspace) "Reklama" menyusini va standart asboblar paneliga
  "Reklama Maket" tugmasini qo'shadi. Har bir workspace uchun bir marta bajariladi:
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
    <!-- "Reklama" menyusi -->
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='b61f72cb-d237-4f8a-9444-845ab4740cbe']"/>
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

  <!-- Asosiy menyuga "Reklama" bo'limini qo'shish -->
  <xsl:template match="commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']/menu">
    <xsl:apply-templates mode="insert-item" select=".">
      <xsl:with-param name="content">
        <xsl:if test="not(./item[@guidRef='7711b833-93ff-468c-9c89-ec2cff226a5a'])">
          <item guidRef="7711b833-93ff-468c-9c89-ec2cff226a5a"/>
        </xsl:if>
      </xsl:with-param>
      <xsl:with-param name="after" select="'47cc9a2d-0b7a-4df1-a686-ea3aa21b4631'"/>
    </xsl:apply-templates>
  </xsl:template>

  <!-- Standart asboblar panelining oxiriga "Reklama Maket" tugmasi -->
  <xsl:template match="commandBarData[@guid='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']/toolbar">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='dbac5bef-29c2-4fba-a4f9-79809933b8c0'])">
        <item guidRef="dbac5bef-29c2-4fba-a4f9-79809933b8c0"/>
      </xsl:if>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
