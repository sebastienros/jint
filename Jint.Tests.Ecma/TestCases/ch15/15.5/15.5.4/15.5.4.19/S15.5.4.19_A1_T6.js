// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.toLocaleUpperCase()
 *
 * @path ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T6.js
 * @description Call toLocaleUpperCase() function of Number.NEGATIVE_INFINITY
 */

Number.prototype.toLocaleUpperCase = String.prototype.toLocaleUpperCase;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if ((Number.NEGATIVE_INFINITY).toLocaleUpperCase() !== "-INFINITY") {
  $ERROR('#1: Number.prototype.toLocaleUpperCase = String.prototype.toLocaleUpperCase; (Number.NEGATIVE_INFINITY).toLocaleUpperCase() === "-INFINITY". Actual: '+(Number.NEGATIVE_INFINITY).toLocaleUpperCase() );
}
//
//////////////////////////////////////////////////////////////////////////////


