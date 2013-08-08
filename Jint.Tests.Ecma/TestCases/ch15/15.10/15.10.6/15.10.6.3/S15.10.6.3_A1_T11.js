// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Equivalent to the expression RegExp.prototype.exec(string) != null
 *
 * @path ch15/15.10/15.10.6/15.10.6.3/S15.10.6.3_A1_T11.js
 * @description RegExp is /2|12/ and tested string is new Number(1.012)
 */

var __string = new Number(1.012);
__re = /2|12/;

//CHECK#0
if (__re.test(__string) !== (__re.exec(__string) !== null)) {
	$ERROR('#0: var __string = new Number(1.012); __re = /2|12/; __re.test(__string) === (__re.exec(__string) !== null)');
}


