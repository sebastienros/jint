// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * RegExp instance has not [[call]] property
 *
 * @path ch15/15.10/15.10.7/S15.10.7_A1_T1.js
 * @description Checking if call of RegExp instance fails
 */

//CHECK#1
try {
	$ERROR('#1.1: /[^a]*/() throw TypeError. Actual: ' + (/[^a]*/()));
} catch (e) {
	if ((e instanceof TypeError) !== true) {
		$ERROR('#1.2: /[^a]*/() throw TypeError. Actual: ' + (e));
	}
}


