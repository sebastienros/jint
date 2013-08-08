// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.lastIndexOf(searchString, position)
 *
 * @path ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T1.js
 * @description Arguments are false and true, and instance is object
 */

var __instance = new Object(true);

__instance.lastIndexOf = String.prototype.lastIndexOf;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (__instance.lastIndexOf(true, false) !== 0) {
  $ERROR('#1: __instance = new Object(true); __instance.lastIndexOf = String.prototype.lastIndexOf;  __instance.lastIndexOf(true, false) === 0. Actual: '+__instance.lastIndexOf(true, false) );
}
//
//////////////////////////////////////////////////////////////////////////////

