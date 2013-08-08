// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * RegExp.prototype.exec(string) Performs a regular expression match of ToString(string) against the regular expression and
 * returns an Array object containing the results of the match, or null if the string did not match
 *
 * @path ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T9.js
 * @description String is undefined variable and RegExp is /1|12/
 */

var __string;

//CHECK#1
__re = /1|12/;
if (__re.exec(__string) !== null) {
	$ERROR('#1: var __string; /1|12/.exec(__string) === null; function __string(){}. Actual: ' + (__re));
}

function __string(){};

