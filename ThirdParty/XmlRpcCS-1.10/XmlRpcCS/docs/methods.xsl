<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

<!-- This XSL file converts the XML output of Microsolf's csc'd /doc option -->
<!-- into data usable by system.methodHelp -->

<xsl:template match="/">
<methodHelp>
<xsl:for-each select="//member[starts-with(@name,'M:') and not(contains(@name,'#ctor'))]">
<xsl:element name="method">
<xsl:attribute name="name"><xsl:value-of select="substring-after(substring-before(concat(@name,'('),'('),'M:')"/></xsl:attribute>

<!-- Get this method's description -->
<xsl:apply-templates select="child::summary"/> 
<xsl:apply-templates select="child::remarks"/>

<!-- Traverse this method's params -->
<xsl:if test="child::param">
<table cellspacing="5" border="0">
	<tr><td><i><b>Parameters</b></i></td></tr>
	<xsl:for-each select="child::param">
		<tr><td><i><xsl:value-of select="@name"/></i></td><td><xsl:apply-templates/></td></tr>
	</xsl:for-each>
</table>
</xsl:if>

<!-- Traverse this method's return -->
<xsl:if test="child::returns">
	<table cellspacing="5" border="0">
	<tr><td><i><b>Returns</b></i></td></tr>
	<tr><td><xsl:apply-templates select="child::returns"/></td></tr>
	</table>
</xsl:if>

</xsl:element>

</xsl:for-each>
</methodHelp>
</xsl:template>

<xsl:template match="para"><P><xsl:apply-templates/></P></xsl:template>

<xsl:template match="paramref" xml:space="preserve"> <I><xsl:apply-templates/></I> </xsl:template>

<xsl:template match="c" xml:space="preserve"> <CODE><xsl:apply-templates/></CODE> </xsl:template>

<xsl:template match="code"><PRE><xsl:apply-templates/></PRE></xsl:template>

</xsl:stylesheet>
