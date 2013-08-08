// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Operator "in" uses GetValue
 *
 * @path ch11/11.8/11.8.7/S11.8.7_A2.1_T2.js
 * @description If GetBase(RelationalExpression) is null, throw ReferenceError
 */

//CHECK#1
try {
  MAX_VALUE in Number;
  $ERROR('#1.1: MAX_VALUE in Number throw ReferenceError. Actual: ' + (MAX_VALUE in Number));  
}
catch (e) {
  if ((e instanceof ReferenceError) !== true) {
    $ERROR('#1.2: MAX_VALUE in Number throw ReferenceError. Actual: ' + (e));  
  }
}

