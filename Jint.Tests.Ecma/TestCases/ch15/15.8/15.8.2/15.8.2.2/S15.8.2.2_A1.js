// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If x is NaN, Math.acos(x) is NaN
 *
 * @path ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A1.js
 * @description Checking if Math.acos(NaN) is NaN
 */

// CHECK#1
var x = NaN;
if (!isNaN(Math.acos(x)))
{
	$ERROR("#1: 'var x=NaN; isNaN(Math.acos(x)) === false'");
}

