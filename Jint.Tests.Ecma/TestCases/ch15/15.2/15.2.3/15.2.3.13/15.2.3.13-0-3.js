/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * A newly created object using the Object contructor has its [[Extensible]]
 * property set to true by default (15.2.2.1, step 8).
 *
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-3.js
 * @description Object.isExtensible is true for objects created using the Object constructor
 */


function testcase() {
  var o = new Object();

  if (Object.isExtensible(o) === true) {
    return true;
  }
 }
runTestCase(testcase);
