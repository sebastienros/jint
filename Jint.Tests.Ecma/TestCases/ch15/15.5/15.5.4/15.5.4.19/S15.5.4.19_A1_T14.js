// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.toLocaleUpperCase()
 *
 * @path ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T14.js
 * @description Call toLocaleUpperCase() function for RegExp object
 */

var __reg = new RegExp("abc");
__reg.toLocaleUpperCase = String.prototype.toLocaleUpperCase;
//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (__reg.toLocaleUpperCase() !== "/ABC/") {
  $ERROR('#1: var __reg = new RegExp("abc"); __reg.toLocaleUpperCase = String.prototype.toLocaleUpperCase; __reg.toLocaleUpperCase() === "/ABC/". Actual: '+__reg.toLocaleUpperCase() );
}
//
//////////////////////////////////////////////////////////////////////////////

