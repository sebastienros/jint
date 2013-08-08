// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.lastIndexOf(searchString, position)
 *
 * @path ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T4.js
 * @description Call lastIndexOf(searchString, position) function without arguments of string
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
//since ToString() evaluates to "" lastIndexOf() evaluates to lastIndexOf("",0)
if ("".lastIndexOf() !== -1) {
  $ERROR('#1: "".lastIndexOf() === -1. Actual: '+("".lastIndexOf()) );
}
//
//////////////////////////////////////////////////////////////////////////////

