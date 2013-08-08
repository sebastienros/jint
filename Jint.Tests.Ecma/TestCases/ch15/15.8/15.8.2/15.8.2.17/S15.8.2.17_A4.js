// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If x is equal to -0, Math.sqrt(x) is -0
 *
 * @path ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A4.js
 * @description Checking if Math.sqrt(-0) equals to -0
 */

// CHECK#1
var x = -0;
if (Math.sqrt(x) !== -0)
{
	$ERROR("#1: 'var x=-0; Math.sqrt(x) !== -0'");
}

