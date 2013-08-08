// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * NaN !== NaN
 *
 * @path ch08/8.5/S8.5_A1.js
 * @description Compare NaN with NaN
 */

var x = Number.NaN;
var x_ = Number.NaN;

///////////////////////////////////////////////////////
// CHECK #1
if (x === x_){
  $ERROR('#1: NaN !== NaN ');
}
//
//////////////////////////////////////////////////////////

