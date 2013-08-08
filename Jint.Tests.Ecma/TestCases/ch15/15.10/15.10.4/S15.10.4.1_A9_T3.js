// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If P's characters do not have the form Pattern, then throw a SyntaxError exception
 *
 * @path ch15/15.10/15.10.4/S15.10.4.1_A9_T3.js
 * @description Pattern is "[a--z]"
 */

//CHECK#1
try {
	$ERROR('#1.1: new RegExp("[a--z]") throw SyntaxError. Actual: ' + (new RegExp("[a--z]")));
} catch (e) {
	if ((e instanceof SyntaxError) !== true) {
		$ERROR('#1.2: new RegExp("[a--z]") throw SyntaxError. Actual: ' + (e));
	}
}


