// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * "var" statement within "for" statement is allowed
 *
 * @path ch12/12.2/S12.2_A10.js
 * @description Declaring variable within a "for" IterationStatement
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
try {
	__ind=__ind;
} catch (e) {
    $ERROR('#1: var inside "for" is admitted '+e.message);
}
//
//////////////////////////////////////////////////////////////////////////////

for (var __ind;;){
    break;
}

