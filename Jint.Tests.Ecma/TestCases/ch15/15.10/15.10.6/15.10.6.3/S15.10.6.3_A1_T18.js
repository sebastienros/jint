// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Equivalent to the expression RegExp.prototype.exec(string) != null
 *
 * @path ch15/15.10/15.10.6/15.10.6.3/S15.10.6.3_A1_T18.js
 * @description RegExp is /nd|ne/ and tested string is undefined
 */

__re = /nd|ne/;

//CHECK#0
if (__re.test(undefined) !== (__re.exec(undefined) !== null)) {
	$ERROR('#0: __re = /nd|ne/; __re.test(undefined) === (__re.exec(undefined) !== null)');
}


