// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Literal :: NullLiteral
 *
 * @path ch07/7.8/7.8.1/S7.8.1_A1_T1.js
 * @description Check null === null
 */

//CHECK#1
if (null !== null) {
  $ERROR('#1: null === null');
} 

