// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If x is NaN, Math.abs(x) is NaN
 *
 * @path ch15/15.8/15.8.2/15.8.2.1/S15.8.2.1_A1.js
 * @description Checking if Math.abs(NaN) is NaN
 */

// CHECK#1
var x = NaN;
if (!isNaN(Math.abs(x)))
{
	$ERROR("#1: 'var x=NaN; isNaN(Math.abs(x)) === false'");
}

