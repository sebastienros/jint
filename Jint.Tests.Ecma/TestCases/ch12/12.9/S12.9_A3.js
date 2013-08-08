// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If Expression is omitted, the return value is undefined
 *
 * @path ch12/12.9/S12.9_A3.js
 * @description Return without Expression
 */

__evaluated = (function (){return;})();

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (__evaluated !== undefined) {
	$ERROR('#1: If Expression is omitted, the return value is undefined');
}
//
//////////////////////////////////////////////////////////////////////////////

