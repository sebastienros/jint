// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.toLocaleLowerCase()
 *
 * @path ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T14.js
 * @description Call toLocaleLowerCase() function for RegExp object
 */

var __reg = new RegExp("ABC");
__reg.toLocaleLowerCase = String.prototype.toLocaleLowerCase;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (__reg.toLocaleLowerCase() !== "/abc/") {
  $ERROR('#1: var __reg = new RegExp("ABC"); __reg.toLocaleLowerCase = String.prototype.toLocaleLowerCase; __reg.toLocaleLowerCase() === "/abc/". Actual: '+__reg.toLocaleLowerCase() );
}
//
//////////////////////////////////////////////////////////////////////////////

