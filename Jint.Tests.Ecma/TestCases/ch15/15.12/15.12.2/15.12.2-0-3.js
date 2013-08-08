/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test should be run without any built-ins being added/augmented.
 * The initial value of [[Configurable]] on JSON is true. This means we
 * should be able to delete (8.6.2.5) the stringify and parse properties.
 *
 * @path ch15/15.12/15.12.2/15.12.2-0-3.js
 * @description JSON.parse must be deletable (configurable)
 */


function testcase() {
  var o = JSON;
  var desc = Object.getOwnPropertyDescriptor(o, "parse");
  return desc.configurable === true;
 }
runTestCase(testcase);
