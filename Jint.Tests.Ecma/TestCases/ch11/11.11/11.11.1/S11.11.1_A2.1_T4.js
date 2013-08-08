// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Operator x && y uses GetValue
 *
 * @path ch11/11.11/11.11.1/S11.11.1_A2.1_T4.js
 * @description If ToBoolean(x) is false and GetBase(y) is null, return false
 */

//CHECK#1
if ((false && x) !== false) {
  $ERROR('#1: (false && x) === false');
}

