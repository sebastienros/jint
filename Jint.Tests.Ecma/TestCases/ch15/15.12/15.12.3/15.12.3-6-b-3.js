/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-6-b-3.js
 * @description JSON.stringify treats numeric space arguments less than 1 (-5) the same as emptry string space argument.
 */


function testcase() {
  var obj = {a1: {b1: [1,2,3,4], b2: {c1: 1, c2: 2}},a2: 'a2'};
  return JSON.stringify(obj,null, -5)=== JSON.stringify(obj);  /* emptry string should be same as no space arg */
  }
runTestCase(testcase);
