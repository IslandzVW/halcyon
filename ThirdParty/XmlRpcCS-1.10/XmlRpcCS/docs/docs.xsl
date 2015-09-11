<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
<xsl:output method="html" />
<!-- This XSL file converts the XML output of Microsolf's csc'd /doc option -->
<!-- into somewhat presentable form. I say "somewhat" because the XML file -->
<!-- Microsoft produces does not include all the typing information you need to -->
<!-- do a complete job. It also is one honking big doc and not all XSLT -->
<!-- handlers can split up output. Get NDoc for a more complete solution. I was -->
<!-- stubborn and did this is in pure XSL to learn XSL -->

<!-- The approach used here is simple. Traverse members for clsses and in each -->
<!-- of those traverse methods, properties etc. -->

<xsl:template match="/">
<html>
<header>
<title>Assembly: <xsl:apply-templates select="//assembly" /></title>
</header>
<body>
<A name="index"></A>
<TABLE cellspacing="0" cellpadding="10" width="100%" BGCOLOR="#9999FF" border="1">
<TR><TD WIDTH="100%" ALIGN="LEFT"><H1>Assmebly: <xsl:apply-templates select="//assembly"/></H1></TD></TR>
</TABLE>

<!-- Generate an index of classes -->
<table border="0" width="100%">
<tr><td bgcolor="#000000" width="100%"><h2><font color="#FFFFFF">Class Index</font></h2></td></tr>
</table>

<table cellspacing="5">
<xsl:for-each select="//member[starts-with(@name,'T:')]">
<xsl:sort select="@name" />
<tr>
    <td valign="top"><xsl:call-template name="member-link">
			<xsl:with-param name="member" select="@name"/>
		     </xsl:call-template></td>
    <td><xsl:apply-templates select="child::summary" /></td>
</tr>
</xsl:for-each>
</table>

<!-- Traverse members of type class -->
<xsl:for-each xml:space="preserve" select="//member[starts-with(@name,'T:')]">
<xsl:sort select="@name" />
        <xsl:variable name="class-name" select="substring-after(@name,':')"/>       
	<table border="1" bgcolor="#AAAAFF" cellspacing="0" cellpadding="10" width="100%">
	<tr>
	<td bgcolor="#AAAAFF" width="90%"><xsl:call-template name="member-anchor">
		<xsl:with-param name="member" select="@name"/>
		</xsl:call-template><h2>Class <xsl:value-of select="$class-name"/>
	</h2></td><td bgcolor="#000000" width="10%" align="center"><a
	href="#index"><font color="#FFFFFF">Class Index</font></a></td>
	</tr>
	</table>
	<xsl:call-template name="text"/>

	<!-- Traverse members of type field associated with the current class -->
	<xsl:variable name="F" select="concat('F:',$class-name,'.')"/>
	<xsl:if test="//member[starts-with(@name,$F)]">
		<xsl:call-template name="section-header">
			<xsl:with-param name="section" select="'Fields'"/>
		</xsl:call-template>
	    <table>
	    <xsl:for-each select="//member[starts-with(@name,$F)]">
		    <xsl:sort select="@name" />
		    <tr>
		    <td ALIGN="LEFT" VALIGN="TOP" ><xsl:call-template name="member-anchor">
				                       <xsl:with-param name="member" select="@name"/>
                        	                   </xsl:call-template><i><b><xsl:call-template name="name-without-namespace">
						<xsl:with-param name="string" select="substring-after(@name,':')"/>
					    </xsl:call-template></b></i></td>
		    <td ALIGN="LEFT"><xsl:call-template name="text"/></td>
		    </tr>
	    </xsl:for-each>
	    </table>
	</xsl:if>

	<!-- Traverse members of type property associated with the current class -->
	<xsl:variable name="P" select="concat('P:',$class-name,'.')"/>
	<xsl:if test="//member[starts-with(@name,$P)]">
		<xsl:call-template name="section-header">
			<xsl:with-param name="section" select="'Properties'"/>
		</xsl:call-template>
		<table>
		<xsl:for-each select="//member[starts-with(@name,$P)]">
		    <xsl:sort select="@name" />
		    <tr>
		    <td ALIGN="LEFT" VALIGN="TOP" ><xsl:call-template name="member-anchor">
			         			<xsl:with-param name="member" select="@name"/>
						   </xsl:call-template><i><b><xsl:call-template name="name-without-namespace">
						<xsl:with-param name="string" select="substring-after(@name,':')"/>
					    </xsl:call-template></b></i></td>
		    <td ALIGN="LEFT"><xsl:call-template name="text"/></td>
		    </tr>
		</xsl:for-each>
		</table>
	</xsl:if>

	<!-- Traverse members of type constructor associated with the current class -->
	<xsl:variable name="I" select="concat('M:',$class-name,'.')"/>
	<xsl:if test="//member[starts-with(@name,$I) and contains(@name,'#ctor')]">
		<xsl:call-template name="section-header">
			<xsl:with-param name="section" select="'Constructors'"/>
		</xsl:call-template>
		<xsl:for-each select="//member[starts-with(@name,$I) and contains(@name,'#ctor')]">
	    		<xsl:sort select="@name" />
			<xsl:call-template name="method-format">
				<xsl:with-param name="class-name" select="$class-name"/>
			</xsl:call-template>
		</xsl:for-each>
	</xsl:if>

	<!-- Traverse members of type method associated with the current class -->
	<xsl:variable name="M" select="concat('M:',$class-name,'.')"/>
	<xsl:if test="//member[starts-with(@name,$M) and not(contains(@name,'#ctor'))]">
		<xsl:call-template name="section-header">
			<xsl:with-param name="section" select="'Methods'"/>
		</xsl:call-template>
		<xsl:for-each select="//member[starts-with(@name,$M) and not(contains(@name,'#ctor'))]">
	    		<xsl:sort select="@name" />
			<xsl:call-template name="method-format">
				<xsl:with-param name="class-name" select="$class-name"/>
			</xsl:call-template>
		</xsl:for-each>
	</xsl:if>
</xsl:for-each>

</body>
</html>
</xsl:template>

<!-- Layout for a method/constructor -->
<xsl:template name="method-format">
        <xsl:param name="class-name"/>
	<xsl:call-template name="member-anchor">
		<xsl:with-param name="member" select="@name"/>
	</xsl:call-template>
	<table border="1" bgcolor="#EEEEFF" width="100%"  cellspacing="0" >
		<tr><td bgcolor="#EEEEFF" width="100%"><b><xsl:call-template name="name-without-namespace">
		<xsl:with-param name="string" select="substring-after(@name,':')"/>
		<xsl:with-param name="class" select="$class-name"/>
		</xsl:call-template><xsl:if test="not(contains(@name,'('))"> ( )</xsl:if></b></td></tr>
	</table>
	
	<xsl:call-template name="text"/><br/>

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

	<!-- Traverse this method's exceptions -->
	<xsl:if test="child::exception">
		<table cellspacing="5" border="0">
		<tr><td><i><b>Exceptions</b></i></td></tr>
		<xsl:for-each select="child::exception">
		        <xsl:sort select="@name" />	
			<tr><td valign="top"><i><b><xsl:value-of select="substring-after(@cref,':')"/></b></i></td><td><xsl:apply-templates/></td></tr>
		</xsl:for-each>
		</table>
	</xsl:if>
</xsl:template>

<!-- Layout of a section header -->
<xsl:template name="section-header">
	<xsl:param name="section"/>
	<table border="1" bgcolor="#CCCCFF" width="100%"  cellspacing="0" >
	<tr><td bgcolor="#CCCCFF" width="100%"><h3>Public <xsl:value-of select="$section"/></h3></td></tr>
	</table>
</xsl:template>

<!-- Layout of the text items in a member -->
<xsl:template name="text" xml:space="preserve">
	<xsl:apply-templates select="child::summary"/> <xsl:apply-templates select="child::remarks"/>
	<xsl:if test="child::seealso">
		<br/><b>See Also:</b><xsl:apply-templates select="child::seealso"/>
	</xsl:if>
</xsl:template>

<!-- Create a HTML link for a member name -->
<xsl:template name="member-link">
        <xsl:param name="member"/>
	<xsl:element name="a">
	    <xsl:attribute name="href">#<xsl:value-of select="$member"/></xsl:attribute>
	    <xsl:value-of select="substring-after($member,':')"/>
	</xsl:element>
</xsl:template>

<!-- Create a HTML anchor for a member name -->
<xsl:template name="member-anchor">
        <xsl:param name="member"/>
	<xsl:element name="a">
	    <xsl:attribute name="name"><xsl:value-of select="$member"/></xsl:attribute>
	</xsl:element>
</xsl:template>

<!-- Rip the leading namespace off a symbol name, and replace #ctor if present -->
<xsl:template name="name-without-namespace">
	<xsl:param name="class"/>
	<xsl:param name="string"/>
	<xsl:choose>
	        <xsl:when test="contains($string,'#ctor')">
			<xsl:call-template name="name-without-namespace">
				<xsl:with-param name="string" select="concat($class,substring-after($string,'#ctor'))"/>
			</xsl:call-template>
		</xsl:when>
		<xsl:when test="contains($string,'(')">
			<xsl:call-template name="name-without-namespace">
				<xsl:with-param name="string" select="substring-before($string,'(')"/>
			</xsl:call-template>(<xsl:value-of select="substring-after($string,'(')"/>
		</xsl:when>
		<xsl:when test="not(contains($string,'.'))">
			<xsl:value-of select="$string"/>
		</xsl:when>
		<xsl:otherwise>
		        <xsl:call-template name="name-without-namespace">
				<xsl:with-param name="string" select="substring-after($string,'.')" />
			</xsl:call-template>
		</xsl:otherwise>
	</xsl:choose>
</xsl:template>

<xsl:template match="para"><P><xsl:apply-templates/></P></xsl:template>

<xsl:template match="paramref" xml:space="preserve"> <I><xsl:apply-templates/></I> </xsl:template>

<xsl:template match="c" xml:space="preserve"> <CODE><xsl:apply-templates/></CODE> </xsl:template>

<xsl:template match="code"><PRE><xsl:apply-templates/></PRE></xsl:template>

<xsl:template match="seealso" xml:space="preserve"> <xsl:call-template name="member-link">
			<xsl:with-param name="member" select="@cref"/>
		     </xsl:call-template> </xsl:template>

</xsl:stylesheet>
