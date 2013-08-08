// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If NewExpression or MemberExpression does not implement internal [[Construct]] method, throw TypeError
 *
 * @path ch11/11.2/11.2.2/S11.2.2_A4_T5.js
 * @description Checking Math object case
 */

//CHECK#1
try {
  new Math;
  $ERROR('#1: new Math throw TypeError');	
}
catch (e) {
  if ((e instanceof TypeError) !== true) {
    $ERROR('#1: new Math throw TypeError');	
  }
}

//CHECK#2
try {
  new new Math();
  $ERROR('#2: new new Math() throw TypeError');	
}
catch (e) {
  if ((e instanceof TypeError) !== true) {
    $ERROR('#2: new new Math() throw TypeError');	
  }
}

//CHECK#3
try {
  var x = new Math();
  new x();
  $ERROR('#3: var x = new Math(); new x() throw TypeError'); 
}
catch (e) {
  if ((e instanceof TypeError) !== true) {
    $ERROR('#3: var x = new Math(); new x() throw TypeError'); 
  }
}


