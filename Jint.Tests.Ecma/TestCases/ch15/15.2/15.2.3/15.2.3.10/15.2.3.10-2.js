/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * The effect of preventExtentions must be testable by calling isExtensible
 *
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-2.js
 * @description Object.preventExtensions returns its arguments after setting its extensible property to false
 */


function testcase() {
  var o  = {};
  var o2 = undefined;

  o2 = Object.preventExtensions(o);
  if (o2 === o && Object.isExtensible(o2) === false) {
    return true;
  }
 }
runTestCase(testcase);
