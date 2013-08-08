// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * VariableDeclaration within "do-while" loop is allowed
 *
 * @path ch12/12.2/S12.2_A12.js
 * @description Declaring variable within "do-while" statement
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
try {
	x=x;
} catch (e) {
	$ERROR('#1: Declaration variable inside "do-while" statement is admitted');
}
//
//////////////////////////////////////////////////////////////////////////////

do var x; while (false);

