/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-7-a-1.js
 * @description JSON.stringify only uses the first 10 characters of a string space arguments.
 */


function testcase() {
  var obj = {a1: {b1: [1,2,3,4], b2: {c1: 1, c2: 2}},a2: 'a2'};
  return JSON.stringify(obj,null, '0123456789xxxxxxxxx')=== JSON.stringify(obj,null, '0123456789');  
  }
runTestCase(testcase);
