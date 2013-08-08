// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Error has property prototype
 *
 * @path ch15/15.11/15.11.3/S15.11.3.1_A4_T1.js
 * @description Checking Error.hasOwnProperty('prototype')
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (!(Error.hasOwnProperty('prototype'))) {
  $ERROR('#1: Error.hasOwnProperty(\'prototype\') return true. Actual: '+Error.hasOwnProperty('prototype'));
}
//
//////////////////////////////////////////////////////////////////////////////

