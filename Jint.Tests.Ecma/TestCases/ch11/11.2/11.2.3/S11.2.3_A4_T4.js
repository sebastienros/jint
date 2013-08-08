// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If MemberExpression does not implement the internal [[Call]] method, throw TypeError
 *
 * @path ch11/11.2/11.2.3/S11.2.3_A4_T4.js
 * @description Checking Global object case
 */

//CHECK#1
try {
  this();
  $ERROR('#1.1: this() throw TypeError. Actual: ' + (this()));	
}
catch (e) {
  if ((e instanceof TypeError) !== true) {
    $ERROR('#1.2: this() throw TypeError. Actual: ' + (e));	
  }
}

