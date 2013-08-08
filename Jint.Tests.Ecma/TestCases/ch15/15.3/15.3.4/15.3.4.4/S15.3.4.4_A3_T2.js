// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If thisArg is null or undefined, the called function is passed the global object as the this value
 *
 * @path ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T2.js
 * @description Argument at call function is null
 */

Function("this.field=\"green\"").call(null);

//CHECK#1
if (this["field"] !== "green") {
  $ERROR('#1: If thisArg is null or undefined, the called function is passed the global object as the this value');
}

