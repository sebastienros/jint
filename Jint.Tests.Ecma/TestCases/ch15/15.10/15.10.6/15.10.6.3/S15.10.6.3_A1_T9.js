// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Equivalent to the expression RegExp.prototype.exec(string) != null
 *
 * @path ch15/15.10/15.10.6/15.10.6.3/S15.10.6.3_A1_T9.js
 * @description RegExp is /1|12/ and tested string is function object
 */

var __string;
__re = /1|12/;

//CHECK#0
if (__re.test(__string) !== (__re.exec(__string) !== null)) {
	$ERROR('#0: var __string;__re = /1|12/; __re.test(__string) === (__re.exec(__string) !== null); function __string(){};');
}

function __string(){};

