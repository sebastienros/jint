/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-8-a-4.js
 * @description JSON.stringify treats an Boolean wrapper space argument the same as a missing space argument.
 */


function testcase() {
  var obj = {a1: {b1: [1,2,3,4], b2: {c1: 1, c2: 2}},a2: 'a2'};
  return JSON.stringify(obj)=== JSON.stringify(obj,null, new Boolean(true));
  }
runTestCase(testcase);
