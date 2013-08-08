// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If x is 1, Math.log(x) is +0
 *
 * @path ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A4.js
 * @description Checking if Math.log(1) equals to +0
 */

// CHECK#1
var x = 1;
if (Math.log(x) !== +0)
{
	$ERROR("#1: 'var x=1; Math.log(x) !== +0'");
}

